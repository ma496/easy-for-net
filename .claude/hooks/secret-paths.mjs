/**
 * Which files hold live credentials — one list, read by every guard that needs it.
 *
 * The write guard and the read guard used to disagree with each other and with the shell
 * guard: writes protected `.env` and `.env.local` only, so `.env.production` could be
 * overwritten, and nothing at all stopped the Read tool opening a per-environment settings
 * file that the shell guard refuses to `cat`. A secret is a secret whichever tool reaches it.
 *
 * Paths arrive normalised to forward slashes.
 */

/** Every dotenv file — `.env`, `.env.local`, `.env.production`, … — except shared templates. */
const DOTENV = /(^|\/)\.env(\.[\w-]+)*$/;
const TEMPLATE = /\.example$/;

export const SECRETS = [
  {
    test: (p) => DOTENV.test(p) && !TEMPLATE.test(p),
    what: "holds live secrets",
    instead: "Use .env.example, and ask the user for any value you need from the real file.",
  },
  {
    test: (p) => /(^|\/)appsettings\.(Development|Testing|Production|Staging)\.json$/i.test(p),
    what: "is a per-machine settings file holding live credentials, and it is not in source control",
    instead: "Use appsettings.json, which carries safe defaults, and ask the user for any real value.",
  },
];

/** The secret rule a path falls under, or null. */
export function secretRuleFor(rawPath) {
  const path = String(rawPath ?? "").replace(/\\/g, "/");
  if (!path) return null;
  return SECRETS.find((rule) => rule.test(path)) ?? null;
}
