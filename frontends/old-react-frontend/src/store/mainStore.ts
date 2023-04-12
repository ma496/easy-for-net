import {create} from "zustand";

interface User {
  id: number,
  name: string,
  email: string
  username: string
}

interface MainStore {
  token?: string | null
  user?: User | null
  setToken: (token?: string | null) => void;
  setUser: (user?: User | null) => void;
}

export const useMainStore = create<MainStore>(set => ({
  setToken: t => set(state => ({
    ...state,
    token: t
  })),
  setUser: u => set(state => ({
    ...state,
    user: u
  })),
}));
