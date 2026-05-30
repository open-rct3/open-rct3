import lume from "lume";
import postcss from "lume/plugins/postcss.ts";
import sass from "lume/plugins/sass.ts";
import sourceMaps from "lume/plugins/source_maps.ts";

const isGitHubActions = Deno.env.get("GITHUB_ACTIONS") === "true";

const site = lume({
  cwd: import.meta.dirname ?? Deno.cwd(),
  src: "./src",
  includes: "templates"
});

// Static data
site.data("siteTitle", "OpenRCT3");
site.data("copyright", `2024-${new Date().getFullYear()}`);
site.data("baseUrl", isGitHubActions ? "https://open-rct3.github.io/open-rct3" : "/");
site.data("forumUrl", "https://github.com/open-rct3/open-rct3/discussions");
site.data("wikiUrl", "https://github.com/open-rct3/open-rct3/wiki");

site.copy("public", ".");

// Plugins
site.use(sass());
site.use(postcss());

// Generate source maps
site.use(sourceMaps({ inline: true }));

export default site;
