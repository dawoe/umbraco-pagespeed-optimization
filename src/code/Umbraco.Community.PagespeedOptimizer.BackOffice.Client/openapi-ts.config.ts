import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
    input: 'swagger.json',
    output: 'src/api',
    plugins: [
        {
            name: '@hey-api/client-fetch',
            // @hey-api/openapi-ts 0.99.0 resolves this path against the
            // process's working directory (this package's root), not
            // against `output` above, unlike older versions. It must be
            // written relative to the working directory so the emitted
            // import in src/api/client.gen.ts correctly resolves to
            // src/hey-api.ts. Do not "simplify" this back to '../hey-api.ts' -
            // that was correct for older openapi-ts versions but breaks the
            // build under 0.99.0 (TS2307, module not found).
            runtimeConfigPath: './src/hey-api.ts',
        },
        {
            name: '@hey-api/sdk',
            asClass: true,
        },
    ],
});