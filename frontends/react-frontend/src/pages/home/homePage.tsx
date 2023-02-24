import {useMainStore} from "../../store/mainStore";

import React from 'react';
import {Flex, Text} from "@mantine/core";


export function HomePage() {
  const token = useMainStore(s => s.token);
  const user = useMainStore(s => s.user);

  return (
    <Flex direction={"column"} p={20} gap={40}>
      <Text w={500} lineClamp={5}>{token}</Text>
      <Text>{JSON.stringify(user)}</Text>
    </Flex>
  );
}
