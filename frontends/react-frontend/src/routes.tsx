import {createBrowserRouter,} from "react-router-dom";
import {LoginPage} from "./pages/login/loginPage";
import {RegisterPage} from "./pages/register/registerPage";
import {HomePage} from "./pages/home/homePage";
import {App} from "./App";

export const router = createBrowserRouter([
  {
    path: "/",
    element: <App />,
    children: [
      {
        index: true,
        element: <HomePage/>,
      },
    ]
  },
  {
    path: "/login",
    element: <LoginPage/>
  },
  {
    path: "/register",
    element: <RegisterPage/>
  },
]);
