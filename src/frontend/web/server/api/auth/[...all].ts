import { auth } from "../../utils/auth";
import { toWebRequest } from "h3";

export default defineEventHandler(async (event) => {
   try {
      const req = toWebRequest(event);
      const res = await auth.handler(req);
      if (res.status === 401 && (event.path || '').includes('/organization/get-full-organization')) {
         return new Response(JSON.stringify(null), {
            status: 200,
            headers: { 'Content-Type': 'application/json' }
         });
      }
      if (res.status >= 500 && (event.path || '').includes('/get-session')) {
         return new Response(JSON.stringify(null), {
            status: 200,
            headers: { 'Content-Type': 'application/json' }
         });
      }
      return res;
   } catch (err: any) {
      if ((event.path || '').includes('/get-session')) {
         return new Response(JSON.stringify(null), {
            status: 200,
            headers: { 'Content-Type': 'application/json' }
         });
      }
      throw err;
   }
});
