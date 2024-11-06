declare module '*.wgsl' {
  type ParsedBundle = import('npm:@use-gpu/shader').ParsedBundle;
  const __module: ParsedBundle;
  export default __module;
}
