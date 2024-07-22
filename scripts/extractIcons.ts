import icns2ico from "npm:icns2ico";
import * as fs from "node:fs";
import * as path from "node:path";

// deno run --allow-read --allow-write scripts/extractIcons.ts -- assets/RCT3.icns

const inputs = Deno.args.slice(1).map(file => path.resolve(Deno.cwd(), file));
console.log(inputs);
inputs.map(file => {
  const icoName = path.basename(file).split('.')[0];
  icns2ico(file).map(({ size, png }) => {
    if (png) fs.writeFileSync(`./${icoName}_${size}.ico`, png);
  })
})
