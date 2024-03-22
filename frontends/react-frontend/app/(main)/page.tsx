import { useCheckoneQuery } from "@/redux/api/checkoneApi";

const { data } = useCheckoneQuery('dd')

const Home = () => {
  return (
    <h1>Home</h1>
  )
}

export default Home;
