import {
  firebaseReady,
  initFirebase,
  watchAuth,
  signInGuest,
  signInWithGoogle,
  registerEmail,
  loginEmail,
  logoutFirebase,
  getProfile,
  updateDisplayName,
} from "./app.js";

export function mountProfileUI({ root, onProfile }) {
  const panel = document.createElement("div");
  panel.className = "profile-panel";
  panel.innerHTML = `
    <div class="profile-card" id="profile-signed-out">
      <p class="profile-title">Player profile</p>
      <p class="profile-sub" id="firebase-status">Connecting…</p>
      <div class="profile-actions">
        <button type="button" class="btn btn-google" id="btn-google">Sign in with Google</button>
        <p class="profile-note">Android APK uses <strong>Google Play Games</strong> sign-in. Web uses Google.</p>
        <button type="button" class="btn btn--secondary btn--compact" id="btn-guest">Continue as Guest</button>
      </div>
      <div class="auth-form">
        <input id="auth-email" type="email" placeholder="Email" autocomplete="username" />
        <input id="auth-pass" type="password" placeholder="Password" autocomplete="current-password" />
        <input id="auth-name" type="text" placeholder="Display name" maxlength="24" />
        <input id="auth-user" type="text" placeholder="Username" maxlength="20" />
        <div class="join-row">
          <button type="button" class="btn btn--secondary btn--compact" id="btn-login">Log in</button>
          <button type="button" class="btn btn--primary btn--compact" id="btn-register">Create profile</button>
        </div>
      </div>
      <p class="hint" id="auth-error"></p>
    </div>
    <div class="profile-card hidden" id="profile-signed-in">
      <p class="profile-title" id="profile-name">Player</p>
      <p class="profile-sub" id="profile-meta">@user</p>
      <p class="profile-stats" id="profile-stats"></p>
      <div class="join-row">
        <input id="rename-input" type="text" maxlength="24" placeholder="New display name" />
        <button type="button" class="btn btn--secondary btn--compact" id="btn-rename">Save</button>
      </div>
      <button type="button" class="btn btn--ghost btn--compact" id="btn-logout">Sign out</button>
    </div>
  `;
  root.prepend(panel);

  const err = panel.querySelector("#auth-error");
  const signedOut = panel.querySelector("#profile-signed-out");
  const signedIn = panel.querySelector("#profile-signed-in");
  const status = panel.querySelector("#firebase-status");

  function setError(msg) {
    err.textContent = msg || "";
    err.style.color = msg ? "var(--danger)" : "";
  }

  async function refreshProfile(user) {
    if (!user) {
      signedOut.classList.remove("hidden");
      signedIn.classList.add("hidden");
      onProfile?.(null);
      return;
    }
    const profile = await getProfile(user.uid);
    signedOut.classList.add("hidden");
    signedIn.classList.remove("hidden");
    panel.querySelector("#profile-name").textContent = profile?.displayName || user.displayName || "Player";
    const provider =
      user.providerData?.some((p) => p.providerId === "google.com")
        ? "Google"
        : user.providerData?.some((p) => p.providerId === "playgames.google.com")
          ? "Play Games"
          : user.isAnonymous
            ? "Guest"
            : user.email || "Account";
    panel.querySelector("#profile-meta").textContent = profile
      ? `@${profile.username} · ${provider}`
      : user.uid;
    const s = profile?.stats || {};
    panel.querySelector("#profile-stats").textContent =
      `Checkmates ${s.checkmatesScored || 0} · Matches ${s.matchesWon || 0}W / ${s.matchesLost || 0}L · Games ${s.gamesPlayed || 0}`;
    onProfile?.(profile || { uid: user.uid, displayName: user.displayName });
  }

  if (!firebaseReady()) {
    status.textContent = "Firebase is not configured yet. Auth will unlock after project setup.";
    panel.querySelectorAll("button, input").forEach((el) => {
      el.disabled = true;
    });
    return;
  }

  initFirebase();
  status.textContent = "Sign in to save your profile and play online.";

  panel.querySelector("#btn-google").addEventListener("click", async () => {
    try {
      setError("");
      await signInWithGoogle();
    } catch (e) {
      setError(e.code === "auth/popup-closed-by-user" ? "Google sign-in cancelled." : (e.message || String(e)));
    }
  });

  panel.querySelector("#btn-guest").addEventListener("click", async () => {
    try {
      setError("");
      await signInGuest();
    } catch (e) {
      setError(e.message || String(e));
    }
  });

  panel.querySelector("#btn-login").addEventListener("click", async () => {
    try {
      setError("");
      const email = panel.querySelector("#auth-email").value.trim();
      const pass = panel.querySelector("#auth-pass").value;
      await loginEmail(email, pass);
    } catch (e) {
      setError(e.message || String(e));
    }
  });

  panel.querySelector("#btn-register").addEventListener("click", async () => {
    try {
      setError("");
      const email = panel.querySelector("#auth-email").value.trim();
      const pass = panel.querySelector("#auth-pass").value;
      const name = panel.querySelector("#auth-name").value.trim();
      const user = panel.querySelector("#auth-user").value.trim();
      await registerEmail(email, pass, name, user);
    } catch (e) {
      setError(e.message || String(e));
    }
  });

  panel.querySelector("#btn-logout").addEventListener("click", () => logoutFirebase());

  panel.querySelector("#btn-rename").addEventListener("click", async () => {
    try {
      setError("");
      await updateDisplayName(panel.querySelector("#rename-input").value);
      await refreshProfile(await import("./app.js").then((m) => m.auth?.currentUser));
    } catch (e) {
      setError(e.message || String(e));
    }
  });

  watchAuth((user) => {
    refreshProfile(user).catch((e) => setError(e.message || String(e)));
  });
}
