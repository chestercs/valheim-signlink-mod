// Drives the SignLink import page (mod/SignLink/Web/index.html) in jsdom against the mock API.
import { JSDOM } from "jsdom";
import fs from "node:fs";

const BASE = process.env.BASE || "http://signlink-mock:8000";
const html = fs.readFileSync("/page/index.html", "utf8");
const dom = new JSDOM(html, {
  url: BASE + "/", runScripts: "dangerously", pretendToBeVisual: true,
  beforeParse(window) {
    window.fetch = (path, opts) => fetch(new URL(path, BASE), opts);
    window.URL.createObjectURL = () => "blob:test";
    window.localStorage.setItem("signlink.pw", "stale-password");   // remembered, then a new one made in the game (0.20.132)
  },
});
const { window } = dom;
const $ = id => window.document.getElementById(id);
const sleep = ms => new Promise(r => setTimeout(r, ms));
let failures = 0;
const check = (ok, what) => { console.log((ok ? "ok   " : "FAIL ") + what); if (!ok) failures++; };

await sleep(500);
check($("version").textContent.includes("0.4.0"), "version shown: " + $("version").textContent);
check(window.localStorage.getItem("signlink.pw") === null && $("pw").value === "" && !$("remember").checked &&
      $("state").className === "bad" && /password/i.test($("state").textContent),
      "a remembered password the API refuses is forgotten: " + $("state").textContent);

$("connect").click();
await sleep(200);
check($("state").className === "bad" && /password/i.test($("state").textContent), "no password: " + $("state").textContent);

$("pw").value = "wrong";
$("connect").click();
await sleep(400);
check($("state").className === "bad" && /password/i.test($("state").textContent), "wrong password rejected: " + $("state").textContent);

$("pw").value = "test-password";
$("remember").checked = true;
$("connect").click();
await sleep(400);
check($("state").className === "ok", "connected: " + $("state").textContent);
check(window.localStorage.getItem("signlink.pw") === "test-password", "remember in this browser still keeps a good one");

$("near").click();
await sleep(400);
const rows = window.document.querySelectorAll(".sign");
check(rows.length === 2, "nearby signs listed: " + rows.length);
rows[1].click();
check($("chosen").textContent.includes("1:93367"), "chosen by click: " + $("chosen").textContent);

$("hovered").click();
await sleep(400);
check($("chosen").textContent.includes("1:93311"), "sign under the crosshair: " + $("chosen").textContent);

$("place").click();
await sleep(300);
check(/choose a picture/.test($("log").textContent), "refuses without a picture");

const png = fs.readFileSync("/m/out/test_gradient.png");
const file = new window.File([png], "test_gradient.png", { type: "image/png" });
const input = $("file");
Object.defineProperty(input, "files", { value: [file] });
input.dispatchEvent(new window.Event("change"));
$("pitch").value = "0.75";
$("tilt").value = "90";
$("out").value = "1.5";
$("dry").click();
await sleep(800);
check(/^Would be 499 x 395 pixels, 9\.4 x 7\.4 m, on 7 sign/.test($("result").textContent), "dry run: " + $("result").textContent);

$("place").click();
await sleep(800);
check(/^Placed: 499 x 395/.test($("result").textContent) && /world save/.test($("result").textContent), "placed: " + $("result").textContent);
check(!$("place").disabled && !$("dry").disabled, "buttons enabled again");

console.log(failures ? failures + " FAILURES" : "PAGE OK");
process.exit(failures ? 1 : 0);
