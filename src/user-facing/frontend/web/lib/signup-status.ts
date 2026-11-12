/** `GET /account/signup-status`: whether the site lets a visitor create an account. */
export interface SignupStatusResponse {
  isSignupEnabled: boolean
}

/**
 * Reads whether sign-up is open from what the API answered: `false` only when it says so plainly.
 * No answer, an error answer or a malformed body counts as open - the form is shown and the sign-up
 * endpoint, which refuses with 403 `signupDisabled`, has the last word. Pure, so the fallback is testable.
 *
 * @param body The parsed response body, or `null` when the API could not be asked or answered with an error.
 */
export const isSignupEnabledFrom = (body: unknown): boolean =>
  !(body && typeof body === 'object' && (body as Partial<SignupStatusResponse>).isSignupEnabled === false)

/** One call to action on the home page: where it goes and which of its labels it shows. */
export interface HomeCta {
  href: '/signup' | '/signin' | '/about'
  label: 'getStarted' | 'signIn' | 'learnMore'
}

/** The home page's calls to action for the site's sign-up state. */
export interface HomeCtas {
  /** The hero's primary button. */
  heroPrimary: HomeCta
  /** The hero's secondary button. */
  heroSecondary: HomeCta
  /** The closing section's button, and whether its copy invites creating an account. */
  closing: HomeCta & { invitesSignup: boolean }
}

/**
 * Which calls to action the home page offers: "Get started" to sign-up while it is open; while it is
 * closed the hero offers "Sign in" instead and the closing section points at the About page, so no
 * button leads to a form that would refuse the visitor.
 */
export const homeCtas = (isSignupEnabled: boolean): HomeCtas =>
  isSignupEnabled
    ? {
        heroPrimary: { href: '/signup', label: 'getStarted' },
        heroSecondary: { href: '/about', label: 'learnMore' },
        closing: { href: '/signup', label: 'getStarted', invitesSignup: true },
      }
    : {
        heroPrimary: { href: '/signin', label: 'signIn' },
        heroSecondary: { href: '/about', label: 'learnMore' },
        closing: { href: '/about', label: 'learnMore', invitesSignup: false },
      }
