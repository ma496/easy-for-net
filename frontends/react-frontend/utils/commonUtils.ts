import { Language } from "@/store/themeConfigSlice";

export function getLang(languages: Language[], code: string) {
  const lang = languages.find(x => x.code === code);
  return lang;
}
