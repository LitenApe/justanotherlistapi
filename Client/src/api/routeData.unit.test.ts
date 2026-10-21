import {
  data,
  isRouteErrorResponse,
  UNSAFE_ErrorResponseImpl as ErrorResponse,
} from "react-router";
import { describe, expect, it } from "vitest";
import { ApiError } from "./http";
import { loadRouteData, routeErrorMessage } from "./routeData";

const request = new Request("http://localhost:5173/lists/1?filter=done");

/** Runs the loader helper and returns whatever it throws. */
async function thrown(error: Error): Promise<unknown> {
  try {
    await loadRouteData(request, () => Promise.reject(error));
  } catch (caught) {
    return caught;
  }
  throw new Error("Expected loadRouteData to throw.");
}

describe("loadRouteData", () => {
  it("passes data through and gives the loader the request's abort signal", async () => {
    let received: AbortSignal | undefined;
    const result = await loadRouteData(request, (signal) => {
      received = signal;
      return Promise.resolve({ lists: [] });
    });

    expect(result).toEqual({ lists: [] });
    expect(received).toBe(request.signal);
  });

  it("redirects to sign-in on 401, returning to the same page afterwards", async () => {
    const response = await thrown(new ApiError(401, "Expired"));

    expect(response).toBeInstanceOf(Response);
    expect((response as Response).status).toBe(302);
    expect((response as Response).headers.get("Location")).toBe(
      "/sign-in?redirectTo=%2Flists%2F1%3Ffilter%3Ddone",
    );
  });

  it("turns other API errors into error responses with the HTTP status", async () => {
    expect(await thrown(new ApiError(403, "No access"))).toEqual(
      data({ message: "No access" }, { status: 403 }),
    );
    expect(await thrown(new ApiError(0, "Can't reach the server."))).toEqual(
      data({ message: "Can't reach the server." }, { status: 503 }),
    );
  });

  it("rethrows anything that isn't an API error", async () => {
    const bug = new TypeError("bug");
    expect(await thrown(bug)).toBe(bug);
  });
});

describe("routeErrorMessage", () => {
  it("prefers the message from the error response", () => {
    const withMessage = new ErrorResponse(403, "Forbidden", { message: "No access" });
    const withoutMessage = new ErrorResponse(500, "Server Error", null);

    expect(isRouteErrorResponse(withMessage)).toBe(true);
    expect(routeErrorMessage(withMessage)).toBe("No access");
    expect(routeErrorMessage(withoutMessage)).toBe("Something went wrong (HTTP 500).");
    expect(routeErrorMessage(new Error("Boom"))).toBe("Boom");
    expect(routeErrorMessage("??")).toBe("Something went wrong.");
  });
});
