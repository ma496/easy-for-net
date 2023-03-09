import {useMainStore} from "../store/mainStore";

export function isAuthenticated() {
  const token = useMainStore(s => s.token);

  return !!token;
}
