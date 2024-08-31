import site from "./config.ts";

if (import.meta.main) await site.build();
