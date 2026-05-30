import lume from "lume";
import postcss from "lume/plugins/postcss.ts";
import sass from "lume/plugins/sass.ts";
import sourceMaps from "lume/plugins/source_maps.ts";

const site = lume({
  cwd: import.meta.dirname ?? Deno.cwd(),
  src: "./src",
  includes: "templates"
});

// Static data
site.data("siteTitle", "OpenRCT3");
site.data("copyright", `2024-${new Date().getFullYear()}`);

site.copy("public", ".");

// Plugins
site.use(sass());
site.use(postcss());

// Generate source maps
site.use(sourceMaps({ inline: true }));

export default site;
