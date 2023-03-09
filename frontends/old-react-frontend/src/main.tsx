import React from 'react'
import ReactDOM from 'react-dom/client'
import './index.css'
import {RouterProvider} from "react-router-dom";
import {router} from "./routes";
import {MantineProvider} from "@mantine/core";
import {QueryClient, QueryClientProvider} from "react-query";
import {ModalsProvider} from "@mantine/modals";
import {NotificationsProvider} from "@mantine/notifications";
import {showError} from "./apiErrorHandling";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 0,
      refetchOnWindowFocus: false,
      onError: (err: any) => {
        showError(err);
      }
    },
    mutations: {
      onError: (err: any) => {
        showError(err);
      }
    }
  },
});

ReactDOM.createRoot(document.getElementById('root') as HTMLElement).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <MantineProvider withGlobalStyles withNormalizeCSS theme={{
        primaryColor: 'blue'
      }}>
        <ModalsProvider>
          <NotificationsProvider>
            <RouterProvider router={router} />
          </NotificationsProvider>
        </ModalsProvider>
      </MantineProvider>
    </QueryClientProvider>
  </React.StrictMode>,
)
