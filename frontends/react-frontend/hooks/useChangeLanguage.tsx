import { toggleRTL } from "@/store/themeConfigSlice";
import { usePathname, useRouter } from "next/navigation";
import { useDispatch } from "react-redux";

const useChangeLanguage = () => {
  const dispatch = useDispatch();
  const pathname = usePathname();
  const router = useRouter();

  return (code: string) => {
    if (code.toLowerCase() === 'ae' || code.toLowerCase() === 'ur') {
      dispatch(toggleRTL('rtl'));
    } else {
      dispatch(toggleRTL('ltr'));
    }

    if (pathname.length > 1) {
      let index = 0;
      for (let i = 1; i < pathname.length; i++) {
        if (pathname[i] === '/') {
          index = i;
          break;
        }
        index = i + 1;
      }
      const subStr = pathname.substring(0, index);
      const url = pathname.replace(subStr, `/${code}`);
      router.push(url);
      router.refresh();
    }
  }
};

export default useChangeLanguage;
