import {useMainStore} from "./store/mainStore";
import {useQuery} from "react-query";
import {apiClient} from "./apiClientInstance";
import {Flex, Loader} from "@mantine/core";
import {AdminLayout} from "./components/layout/admin/AdminLayout";

export function App() {
  const setToken = useMainStore(s => s.setToken);
  const setUser = useMainStore(s => s.setUser);
  const {isLoading} = useQuery('getUserForStore',
    () => apiClient.user.getUserGetById(parseInt(localStorage.getItem('uid') ?? '0')),
    {
      onSuccess: (u => {
        setToken(localStorage.getItem('token'));
        setUser({
          id: u?.id ?? 0,
          name: u?.name ?? '',
          email: u?.email ?? '',
          username: u?.username ?? '',
        });
      })
    });

  return isLoading
    ? (
      <Flex justify={"center"} align={"center"} style={{height: '100vh'}}>
        <Loader />
      </Flex>
    )
    : (
      <AdminLayout />
    )
}
