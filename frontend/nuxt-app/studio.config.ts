import { defineStudioConfig } from "better-auth-studio";
import { auth } from "./server/utils/auth";

export default defineStudioConfig({
    auth
});
