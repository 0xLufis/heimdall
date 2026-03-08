import { auth } from "../../utils/auth";

export default defineEventHandler((event) => {
   // Convert the Nitro event into a standard Web Request that Better-Auth understands
   return auth.handler(toWebRequest(event));
}) 
