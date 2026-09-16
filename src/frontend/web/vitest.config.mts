import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'

/**
 * Vitest configuration for the web application.
 *
 * The only thing it adds to the defaults is the `@/` path alias from `tsconfig.json`, which is what
 * the application is written against. TypeScript erases an import that is only used as a type, so a
 * test of a module that imports its dependencies through the alias needs it resolved here as well;
 * without it Vitest reads `@/store/x` as a package name and cannot find it.
 */
export default defineConfig({
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./', import.meta.url)),
    },
  },
  test: {
    include: ['**/*.test.ts'],
    exclude: ['node_modules/**', '.next/**'],
  },
})
