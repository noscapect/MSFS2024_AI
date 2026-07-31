const copyStaticFiles = require("esbuild-copy-static-files");
const globalExternals = require("@fal-works/esbuild-plugin-global-externals");
const { typecheckPlugin } = require("@jgoz/esbuild-plugin-typecheck");
const esbuild = require("esbuild");
const postcss = require("postcss");
const postCssUrl = require("postcss-url");
const postcssPrefixSelector = require("postcss-prefix-selector");
const sassPlugin = require("esbuild-sass-plugin");

require("dotenv").config({ path: __dirname + "/.env" });

const baseConfig = {
  entryPoints: ["src/VfoEfb.tsx"],
  keepNames: true,
  bundle: true,
  outdir: "dist",
  sourcemap: process.env.SOURCE_MAPS === "true",
  minify: process.env.MINIFY === "true",
  logLevel: "info",
  target: "es2017",
  define: {
    BASE_URL: `"coui://html_ui/efb_ui/efb_apps/VfoEfbV6"`,
  },
  plugins: [
    copyStaticFiles({
      src: "./src/Assets",
      dest: "./dist/Assets",
    }),
    globalExternals.globalExternals({
      "@microsoft/msfs-sdk": {
        varName: "msfssdk",
        type: "cjs",
      },
    }),
    sassPlugin.sassPlugin({
      async transform(source) {
        const { css } = await postcss([
          postCssUrl({ url: "copy" }),
          postcssPrefixSelector({
            prefix: ".efb-view.VfoEfbV6",
          }),
        ]).process(source, { from: undefined });
        return css;
      },
    }),
  ],
};

if (process.env.TYPECHECKING === "true") {
  baseConfig.plugins.push(
    typecheckPlugin({ watch: process.env.SERVING_MODE === "WATCH" })
  );
}

if (process.env.SERVING_MODE === "WATCH") {
  esbuild.context(baseConfig).then((context) => context.watch());
} else {
  esbuild.build(baseConfig);
}
