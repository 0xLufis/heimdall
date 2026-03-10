import { auth } from "../../utils/auth";

export default defineEventHandler(async (event) => {
   const request = toWebRequest(event);
   
   // Debugging: Log session info for admin check
   if (event.path.includes('admin/list-users')) {
       const session = await auth.api.getSession({ headers: request.headers });
       console.log("--- DEBUG: ADMIN API CALL ---");
       console.log("Path:", event.path);
       console.log("Session User:", session?.user ? { id: session.user.id, role: (session.user as any).role } : "No Session");
       console.log("-----------------------------");
   }

   return auth.handler(request);
})
