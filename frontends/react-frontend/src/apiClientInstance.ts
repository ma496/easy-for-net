import {AppClient} from "./generatedClient";

export const apiClient = new AppClient({
  BASE: import.meta.env.VITE_API_BASE_URL
});
