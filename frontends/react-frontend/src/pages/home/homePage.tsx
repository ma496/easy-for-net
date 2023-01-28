import {useMainStore} from "../../store/mainStore";
import {Text} from "@mantine/core";

export function HomePage() {
  const token = useMainStore(s => s.token);
  const user = useMainStore(s => s.user);
  return (
    <div style={{padding: 30}}>
      <h1>Token</h1>
      <Text>{token}</Text>
      <h1>User</h1>
      <Text>{JSON.stringify(user)}</Text>
    </div>
  )
}
