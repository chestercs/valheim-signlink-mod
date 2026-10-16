// JS (simulator) vs Python (engine): layout, codec and inverse layout over every built row.
import fs from "fs";
const html = fs.readFileSync("out/simulator.html", "utf8");
const js = html.split("<script>")[1].split("// ---------------------------------------------------------------- touching pixels")[0];
const rows = JSON.parse(fs.readFileSync("out/parity.json", "utf8"));
const fn = new Function("rows", js + `
  let bad = 0;
  for (const r of rows) {
    const g = makeGrid(r.grid), c = parse(r.text), p = layout(c, r.cell, g), enc = encode(c);
    const back = p && encode(toCmd(p, r.cell, g));
    if (!p || Math.abs(p.x - r.x) > 1e-6 || Math.abs(p.y - r.y) > 1e-6 || enc !== r.enc || back !== r.back) {
      bad++; if (bad < 5) console.log("MISMATCH", r, p && [p.x, p.y], enc, back);
    }
  }
  return bad;`);
const bad = fn(rows);
console.log(rows.length, "rows,", bad, "JS mismatches");
process.exit(bad ? 1 : 0);
