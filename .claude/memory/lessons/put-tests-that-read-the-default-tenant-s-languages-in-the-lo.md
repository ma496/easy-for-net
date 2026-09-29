---
scope: localization
learned: 2026-09-29
task: 06-document-session-validation-and-revocation
---

# Put tests that read the default tenant's languages in the Localization collection

Tests in the Localization collection change the default tenant's LanguageSetting (e.g. EnabledCultures=["en"]), so any other class asserting a non-English localized message for the default tenant races them and intermittently gets English. SessionStoreUnavailableTests' 'es' case fails this way; mark such classes [Collection("Localization")] or use a tenant the test created, instead of rerunning until green.
