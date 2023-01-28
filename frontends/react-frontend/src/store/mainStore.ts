import {create} from "zustand";
import {apiClient} from "../apiClientInstance";

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
  init: () => void;
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
  init: async () => {
    const token = localStorage.getItem('token');
    let user: User;
    if (token) {
      const u = await apiClient.user.getUserGetById(parseInt(localStorage.getItem('uid') ?? '0'));
      user = {
        id: u.id ?? 0,
        name: u.name ?? '',
        email: u.email ?? '',
        username: u.username ?? '',
      }
    }
    return set(state => ({
      ...state,
      token,
      user
    }));
  }
}));
