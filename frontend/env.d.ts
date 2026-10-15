/// <reference types="vite/client" />

// Single-file components are compiled by vue-tsc, so no ambient `*.vue` module
// declaration is required for .ts files to import .vue SFCs. This reference is
// kept because it is what registers the static-asset and `import.meta.env`
// module declarations.
