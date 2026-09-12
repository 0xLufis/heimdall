import { defineEventHandler, readBody, getRequestHeaders, type H3Event } from "h3";
import { betterAuthStudio } from "better-auth-studio/nuxt";
import studioConfig from "../../studio.config";

const underlyingHandler = betterAuthStudio(studioConfig);

export const handleStudioEvent = async (event: H3Event) => {
  // Ensure globalThis.readBody is available as expected by better-auth-studio nuxt adapter
  if (typeof (globalThis as any).readBody !== "function") {
    (globalThis as any).readBody = readBody;
  }

  // better-auth-studio's nuxt adapter runs Object.entries(event.headers).
  // Because H3Event.headers is a Web Headers instance whose properties are not own-enumerable,
  // Object.entries returns []. We override event.headers with a plain record so Object.entries works.
  const plainHeaders = getRequestHeaders(event);
  try {
    Object.defineProperty(event, "headers", {
      value: plainHeaders,
      configurable: true,
      writable: true,
      enumerable: true,
    });
  } catch {
    (event as any).headers = plainHeaders;
  }

  // Pre-parse body for mutation methods so convertNuxtToUniversal can read it
  if (["POST", "PUT", "PATCH", "DELETE"].includes(event.method)) {
    if ((event as any).body === undefined) {
      try {
        (event as any).body = await readBody(event);
      } catch {
        // Ignored if body is empty or not JSON
      }
    }
  }

  return underlyingHandler(event);
};

export const createStudioRouteHandler = () => defineEventHandler(handleStudioEvent);
