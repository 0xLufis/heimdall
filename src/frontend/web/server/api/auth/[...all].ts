import { auth } from "../../utils/auth";
import { defineEventHandler, toWebRequest } from "h3";

/**
 * better-auth-studio's event plugin looks the session up by `body.token` before every sign-out.
 * Better-Auth clients send no token in the body, so that lookup ran with an undefined value, the
 * query failed and sign-out answered 500 - leaving the session row (and the cookie) in place.
 * Supplying the caller's own session token (or an empty one when there is no session) lets the
 * hook run; sign-out itself still only acts on the session cookie.
 */
async function withSignOutToken(req: Request): Promise<Request> {
   let token = "";
   try {
      const session = await auth.api.getSession({ headers: req.headers });
      token = session?.session?.token || "";
   } catch {
      // no session: sign-out just clears the cookie
   }
   const headers = new Headers(req.headers);
   headers.set("content-type", "application/json");
   headers.delete("content-length");
   return new Request(req.url, { method: req.method, headers, body: JSON.stringify({ token }) });
}

export default defineEventHandler(async (event) => {
   try {
      let req = toWebRequest(event);
      if (req.method === "POST" && /\/sign-out\/?(\?|$)/.test(event.path || "")) {
         req = await withSignOutToken(req);
      }
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
