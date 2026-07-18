import resolve from "@rollup/plugin-node-resolve";
import commonjs from "@rollup/plugin-commonjs";
import typescript from "@rollup/plugin-typescript";

/** @type {import('rollup').RollupOptions} */
export default {
  input: "src/plugin.ts",
  output: {
    file: "com.pj289.jbl-quantum.sdPlugin/bin/plugin.js",
    format: "es",
    sourcemap: true,
  },
  plugins: [typescript(), resolve({ preferBuiltins: true }), commonjs()],
  external: [],
};
