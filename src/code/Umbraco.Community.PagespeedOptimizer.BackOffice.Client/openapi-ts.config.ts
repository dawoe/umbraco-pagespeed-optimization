import { defineConfig } from '@hey-api/openapi-ts';

export default defineConfig({
    input: 'swagger.json',
    output: 'src/api',
    plugins: [
        {
            name: '@hey-api/client-fetch',
            runtimeConfigPath: './src/hey-api.ts',
        },
        {
            name: '@hey-api/sdk',
            asClass: true,
        },
    ],
});