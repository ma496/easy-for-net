import {useMainStore} from "./store/mainStore";
import {Outlet} from "react-router-dom";

export function App() {
  const init = useMainStore(s => s.init);
  init();
  return <div><Outlet /></div>
}
