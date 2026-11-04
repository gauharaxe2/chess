import { spawn } from "node:child_process";
import { writeFileSync, mkdirSync } from "node:fs";

const htmlPath = "/tmp/chess-smoke.html";
mkdirSync("/tmp/chess-puppeteer", { recursive: true });

// Drive the live server with Chrome DevTools Protocol via puppeteer-core if available,
// otherwise use a minimal chrome --headless script with evaluate through remote debugging.
const port = 9222;
const chrome = spawn(
  "google-chrome",
  [
    "--headless=new",
    "--disable-gpu",
    "--no-sandbox",
    `--remote-debugging-port=${port}`,
    "--user-data-dir=/tmp/chess-chrome-profile",
    "about:blank",
  ],
  { stdio: ["ignore", "pipe", "pipe"] }
);

await new Promise((r) => setTimeout(r, 1200));

async function cdp(method, params = {}, sessionId) {
  const version = await fetch(`http://127.0.0.1:${port}/json/version`).then((r) => r.json());
  const wsUrl = version.webSocketDebuggerUrl;
  const { default: WebSocket } = await import("ws").catch(() => ({ default: null }));
  if (!WebSocket) {
    // Fallback: use HTTP /json/new + Runtime via chrome-remote-interface style with raw WS in node 22?
    throw new Error("ws package missing");
  }
}

// Prefer playwright-core / puppeteer-core from npx
async function runWithPuppeteer() {
  const { createRequire } = await import("node:module");
  // dynamic import from npx cache is hard; install puppeteer-core locally in /tmp
  const { execSync } = await import("node:child_process");
  execSync("npm install --prefix /tmp/chess-puppeteer puppeteer-core@23 --silent", {
    stdio: "inherit",
  });
  const puppeteer = await import("/tmp/chess-puppeteer/node_modules/puppeteer-core/lib/esm/puppeteer/puppeteer-core.js").catch(
    async () => import("/tmp/chess-puppeteer/node_modules/puppeteer-core/lib/cjs/puppeteer/puppeteer-core.js")
  );
  const browser = await puppeteer.connect({
    browserURL: `http://127.0.0.1:${port}`,
    defaultViewport: { width: 900, height: 1400 },
  });
  const page = await browser.newPage();
  page.on("pageerror", (e) => console.error("PAGEERROR", e.message));
  page.on("console", (m) => console.log("CONSOLE", m.type(), m.text()));
  await page.goto("http://127.0.0.1:8765/", { waitUntil: "networkidle0", timeout: 30000 });
  await page.click("#btn-offline");
  await page.waitForSelector('.sq[data-square="e2"]', { timeout: 5000 });
  await page.click('.sq[data-square="e2"]');
  await page.waitForSelector('.sq[data-square="e2"].selected', { timeout: 3000 });
  const legalCount = await page.$$eval(".sq.legal", (els) => els.length);
  await page.click('.sq[data-square="e4"]');
  await page.waitForFunction(() => {
    const status = document.getElementById("status-label")?.textContent || "";
    return status.includes("Black");
  }, { timeout: 3000 });
  const turn = await page.$eval("#turn-label", (el) => el.textContent);
  const status = await page.$eval("#status-label", (el) => el.textContent);
  const e4HasPawn = await page.$eval('.sq[data-square="e4"] .piece', (el) => !!el);
  console.log(JSON.stringify({ ok: true, legalCount, turn, status, e4HasPawn }, null, 2));
  await browser.disconnect();
  chrome.kill("SIGKILL");
}

try {
  await runWithPuppeteer();
  process.exit(0);
} catch (e) {
  console.error("SMOKE_FAIL", e);
  chrome.kill("SIGKILL");
  process.exit(1);
}
