import lume from "lume";
import postcss from "lume/plugins/postcss.ts";
import sass from "lume/plugins/sass.ts";
import sourceMaps from "lume/plugins/source_maps.ts";

const site = lume({
  cwd: import.meta.dirname ?? Deno.cwd(),
  src: "./src",
  includes: "templates"
});
// TODO: https://deno.land/x/lume@v2.2.4/plugins/sass.ts
site.data("siteTitle", "OpenRCT3");
site.copy("js", "js");
site.copy("public", ".");

// Plugins
site.use(sass());
site.use(postcss());

// Generate source maps
site.use(sourceMaps({ inline: true }));

export default site;
