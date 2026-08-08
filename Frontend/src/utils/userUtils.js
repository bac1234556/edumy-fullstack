/**
 * Standard user role constants.
 */
export const USER_ROLES = {
  STUDENT: "Student",
  INSTRUCTOR: "Instructor",
  ADMIN: "Admin"
};

/**
 * Normalizes any role input (string, array, or object) strictly into one of:
 * "Student", "Instructor", "Admin", or "".
 * NEVER defaults unrecognized or missing roles to "Student".
 */
export const normalizeRole = (value) => {
  if (Array.isArray(value)) {
    value = value[0];
  }

  if (typeof value !== "string") {
    return "";
  }

  const role = value.trim().toLowerCase();

  if (role === "student") return USER_ROLES.STUDENT;
  if (role === "instructor") return USER_ROLES.INSTRUCTOR;
  if (role === "admin") return USER_ROLES.ADMIN;

  return "";
};

/**
 * Returns the default dashboard route for a given user role.
 */
export const getDefaultRouteForRole = (role) => {
  const normalized = normalizeRole(role);
  if (normalized === USER_ROLES.INSTRUCTOR) return "/instructor";
  if (normalized === USER_ROLES.ADMIN) return "/admin";
  return "/";
};

/**
 * Checks if a given pathname is allowed for the specified role.
 */
export const isRouteAllowedForRole = (pathname, role) => {
  const normalized = normalizeRole(role);
  const lowerPath = (pathname || "").toLowerCase();

  if (lowerPath.startsWith("/cart") || lowerPath.startsWith("/wishlist") || lowerPath.startsWith("/my-courses")) {
    return normalized === USER_ROLES.STUDENT;
  }
  if (lowerPath.startsWith("/instructor")) {
    return normalized === USER_ROLES.INSTRUCTOR;
  }
  if (lowerPath.startsWith("/admin")) {
    return normalized === USER_ROLES.ADMIN;
  }
  return true;
};

/**
 * Safely converts any value to a trimmed string or returns the fallback.
 */
export const toSafeString = (value, fallback = "") => {
  if (typeof value === "string") {
    const normalized = value.trim();
    return normalized || fallback;
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value).trim() || fallback;
  }

  return fallback;
};

/**
 * Gets a displayable user name candidate or defaults to "User".
 */
export const getDisplayName = (user) => {
  if (!user || typeof user !== "object") {
    return "User";
  }

  const candidates = [
    user.fullName,
    user.displayName,
    user.name,
    user.userName,
    user.profile?.fullName,
    user.email
  ];

  for (const candidate of candidates) {
    const value = toSafeString(candidate);
    if (value) {
      return value;
    }
  }

  return "User";
};

/**
 * Derives uppercase initials from user display name or returns "U".
 */
export const getUserInitials = (user) => {
  const displayName = getDisplayName(user);

  const words = displayName
    .split(/\s+/)
    .filter(Boolean);

  if (words.length === 0) {
    return "U";
  }

  if (words.length === 1) {
    return words[0].charAt(0).toUpperCase() || "U";
  }

  return `${words[0].charAt(0)}${words.at(-1).charAt(0)}`
    .toUpperCase();
};

/**
 * Normalizes any raw user object, DTO, or Axios response payload.
 */
export const normalizeUser = (rawUser) => {
  const source =
    rawUser?.data?.user ??
    rawUser?.data ??
    rawUser?.user ??
    rawUser;

  if (!source || typeof source !== "object" || Array.isArray(source)) {
    return null;
  }

  const id = source.userId ?? source.id ?? null;
  const fullName = toSafeString(
    source.fullName ??
    source.displayName ??
    source.profile?.fullName
  );
  const email = toSafeString(source.email);
  const avatarUrl = toSafeString(
    source.avatarUrl ??
    source.profile?.avatarUrl
  );
  const role = normalizeRole(source.role ?? source.profile?.role);

  return {
    id,
    userId: id,
    fullName,
    email,
    avatarUrl,
    role
  };
};

/**
 * Normalizes notification TargetUrl to ensure safe, same-origin relative frontend path.
 */
export const normalizeNotificationTargetUrl = (value) => {
  if (typeof value !== "string") {
    return "";
  }

  let url = value.trim();
  if (!url) {
    return "";
  }

  if (url.startsWith("http://") || url.startsWith("https://")) {
    try {
      const parsed = new URL(url);
      if (typeof window !== "undefined" && parsed.origin === window.location.origin) {
        url = parsed.pathname + parsed.search + parsed.hash;
      } else {
        return "";
      }
    } catch {
      return "";
    }
  }

  if (url.toLowerCase().startsWith("javascript:") || url.toLowerCase().startsWith("data:")) {
    return "";
  }

  if (!url.startsWith("/")) {
    url = `/${url}`;
  }

  return url;
};
