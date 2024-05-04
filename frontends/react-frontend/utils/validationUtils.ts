import { setLocale } from "yup";

export const setValidationTranslations = (t: any) => {
  const required = t("required", {'path': '{path}'});
  const minString = t("min_string", {'path': '{path}', 'min': '{min}'});
  const maxString = t("max_string", {'path': '{path}', 'max': '{max}'});
  const emailValid = t("email_valid", {'path': '{path}'});
  const minNumber = t("min_number", {'path': '{path}', 'min': '{min}'});
  const maxNumber = t("max_number", {'path': '{path}', 'max': '{max}'});

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
