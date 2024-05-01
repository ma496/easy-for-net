import { setLocale } from "yup";

export const setValidationTranslations = (t: (key: string) => any) => {
  const required = t("required");
  const minString = t("min_string");
  const maxString = t("max_string");
  const emailValid = t("email_valid");
  const minNumber = t("min_number");
  const maxNumber = t("max_number");

  setLocale({
    mixed: {
      required(params) {
        return required.replace('${path}', t(params.path))
      },
    },
    string: {
      min(params) {
        return minString.replace('${path}', t(params.path))
      },
      max(params) {
        return maxString.replace('${path}', t(params.path))
      },
      email(params) {
        return emailValid.replace('${path}', t(params.path))
      },
    },
    number: {
      min(params) {
        return minNumber.replace('${path}', t(params.path))
      },
      max(params) {
        return maxNumber.replace('${path}', t(params.path))
      },
    },
  });
};
