import { initializeApp } from "https://www.gstatic.com/firebasejs/11.0.2/firebase-app.js";
import {
  getAuth,
  onAuthStateChanged,
  signInAnonymously,
  createUserWithEmailAndPassword,
  signInWithEmailAndPassword,
  signInWithPopup,
  GoogleAuthProvider,
  updateProfile,
  signOut,
} from "https://www.gstatic.com/firebasejs/11.0.2/firebase-auth.js";
import {
  getFirestore,
  doc,
  getDoc,
  setDoc,
  updateDoc,
  serverTimestamp,
  collection,
  query,
  where,
  orderBy,
  limit,
  getDocs,
  onSnapshot,
  addDoc,
} from "https://www.gstatic.com/firebasejs/11.0.2/firebase-firestore.js";
import { firebaseConfig, isFirebaseConfigured } from "./config.js";

let app = null;
let auth = null;
let db = null;

export function firebaseReady() {
  return isFirebaseConfigured();
}

export function initFirebase() {
  if (!isFirebaseConfigured()) {
    console.warn("[Firebase] config.js is empty — project not linked yet.");
    return null;
  }
  if (app) return { app, auth, db };
  app = initializeApp(firebaseConfig);
  auth = getAuth(app);
  db = getFirestore(app);
  return { app, auth, db };
}

export function watchAuth(callback) {
  initFirebase();
  if (!auth) {
    callback(null);
    return () => {};
  }
  return onAuthStateChanged(auth, callback);
}

export async function signInGuest() {
  initFirebase();
  const cred = await signInAnonymously(auth);
  await ensureProfile(cred.user, {
    displayName: `Guest${Math.floor(1000 + Math.random() * 9000)}`,
    username: `guest_${cred.user.uid.slice(0, 8).toLowerCase()}`,
  });
  return cred.user;
}

export async function registerEmail(email, password, displayName, username) {
  initFirebase();
  const cred = await createUserWithEmailAndPassword(auth, email, password);
  const cleanName = (displayName || email.split("@")[0]).trim().slice(0, 24);
  const cleanUser = normalizeUsername(username || cleanName);
  await updateProfile(cred.user, { displayName: cleanName });
  await claimUsernameAndCreateProfile(cred.user, cleanName, cleanUser, email);
  return cred.user;
}

export async function loginEmail(email, password) {
  initFirebase();
  const cred = await signInWithEmailAndPassword(auth, email, password);
  await ensureProfile(cred.user, {
    displayName: cred.user.displayName || email.split("@")[0],
    username: normalizeUsername(cred.user.displayName || `player_${cred.user.uid.slice(0, 6)}`),
  });
  return cred.user;
}

/** Google Sign-In (web). On Android/Play Store builds use Google Play Games instead. */
export async function signInWithGoogle() {
  initFirebase();
  const provider = new GoogleAuthProvider();
  provider.setCustomParameters({ prompt: "select_account" });
  provider.addScope("profile");
  provider.addScope("email");
  const cred = await signInWithPopup(auth, provider);
  const name = cred.user.displayName || cred.user.email?.split("@")[0] || "Google Player";
  await ensureProfile(cred.user, {
    displayName: name,
    username: normalizeUsername(`g_${cred.user.uid.slice(0, 8)}`),
  });
  return cred.user;
}

export async function logoutFirebase() {
  initFirebase();
  if (auth?.currentUser) await signOut(auth);
}

function normalizeUsername(raw) {
  return String(raw || "player")
    .toLowerCase()
    .replace(/[^a-z0-9_]/g, "_")
    .replace(/_+/g, "_")
    .replace(/^_|_$/g, "")
    .slice(0, 20)
    .padEnd(3, "0");
}

function emptyStats() {
  return {
    checkmatesScored: 0,
    matchesWon: 0,
    matchesLost: 0,
    gamesPlayed: 0,
    draws: 0,
  };
}

async function claimUsernameAndCreateProfile(user, displayName, username, email) {
  const uname = normalizeUsername(username);
  const unameRef = doc(db, "usernames", uname);
  const existing = await getDoc(unameRef);
  if (existing.exists() && existing.data().uid !== user.uid) {
    throw new Error("Username is already taken");
  }

  await setDoc(unameRef, { uid: user.uid, displayName });
  await setDoc(doc(db, "profiles", user.uid), {
    uid: user.uid,
    displayName,
    username: uname,
    email: email || user.email || "",
    photoURL: user.photoURL || "",
    createdAt: serverTimestamp(),
    updatedAt: serverTimestamp(),
    stats: emptyStats(),
  });
}

async function ensureProfile(user, defaults) {
  initFirebase();
  const ref = doc(db, "profiles", user.uid);
  const snap = await getDoc(ref);
  if (snap.exists()) return snap.data();
  await claimUsernameAndCreateProfile(
    user,
    defaults.displayName,
    defaults.username,
    user.email || ""
  );
  const created = await getDoc(ref);
  return created.data();
}

export async function getProfile(uid) {
  initFirebase();
  const snap = await getDoc(doc(db, "profiles", uid));
  return snap.exists() ? snap.data() : null;
}

export async function updateDisplayName(displayName) {
  initFirebase();
  const user = auth.currentUser;
  if (!user) throw new Error("Not signed in");
  const name = displayName.trim().slice(0, 24);
  if (name.length < 2) throw new Error("Display name too short");
  await updateProfile(user, { displayName: name });
  await updateDoc(doc(db, "profiles", user.uid), {
    displayName: name,
    updatedAt: serverTimestamp(),
  });
  const unameSnap = await getDoc(doc(db, "profiles", user.uid));
  const username = unameSnap.data()?.username;
  if (username) {
    await setDoc(doc(db, "usernames", username), { uid: user.uid, displayName: name }, { merge: true });
  }
}

export async function recordMatchOutcome({ scoredCheckmate, wonMatch, lostMatch, draw }) {
  initFirebase();
  const user = auth.currentUser;
  if (!user) return;
  const ref = doc(db, "profiles", user.uid);
  const snap = await getDoc(ref);
  if (!snap.exists()) return;
  const stats = { ...emptyStats(), ...(snap.data().stats || {}) };
  stats.gamesPlayed += 1;
  if (scoredCheckmate) stats.checkmatesScored += 1;
  if (wonMatch) stats.matchesWon += 1;
  if (lostMatch) stats.matchesLost += 1;
  if (draw) stats.draws += 1;
  await updateDoc(ref, { stats, updatedAt: serverTimestamp() });
}

export async function createMatchDoc(roomCode) {
  initFirebase();
  const user = auth.currentUser;
  if (!user) throw new Error("Sign in required");
  const payload = {
    hostUid: user.uid,
    guestUid: "",
    roomCode: String(roomCode),
    status: "waiting",
    whiteScore: 0,
    blackScore: 0,
    checkmatesToWin: 2,
    lastMove: "",
    moveSeq: 0,
    fen: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
    createdAt: serverTimestamp(),
    updatedAt: serverTimestamp(),
    winnerUid: null,
  };
  const ref = await addDoc(collection(db, "matches"), payload);
  return ref.id;
}

export async function joinMatchByRoomCode(roomCode) {
  initFirebase();
  const user = auth.currentUser;
  if (!user) throw new Error("Sign in required");
  const q = query(
    collection(db, "matches"),
    where("roomCode", "==", String(roomCode)),
    where("status", "==", "waiting"),
    orderBy("createdAt", "desc"),
    limit(1)
  );
  const snaps = await getDocs(q);
  if (snaps.empty) throw new Error("No open match for that room code");
  const matchDoc = snaps.docs[0];
  await updateDoc(matchDoc.ref, {
    guestUid: user.uid,
    status: "active",
    updatedAt: serverTimestamp(),
  });
  return matchDoc.id;
}

export function listenMatch(matchId, onData) {
  initFirebase();
  return onSnapshot(doc(db, "matches", matchId), (snap) => {
    if (snap.exists()) onData({ id: snap.id, ...snap.data() });
  });
}

export async function pushMatchMove(matchId, { uci, fen, moveSeq, whiteScore, blackScore, status, winnerUid }) {
  initFirebase();
  await updateDoc(doc(db, "matches", matchId), {
    lastMove: uci,
    fen,
    moveSeq,
    whiteScore,
    blackScore,
    status: status || "active",
    winnerUid: winnerUid ?? null,
    updatedAt: serverTimestamp(),
  });
}

export { auth, db };
