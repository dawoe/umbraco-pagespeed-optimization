import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import type { CreateClientConfig } from './api/client/types.gen';

export const createClientConfig: CreateClientConfig = (config) => ({
    ...config,
    ...umbHttpClient.getConfig(),
});