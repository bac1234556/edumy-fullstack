import assert from 'node:assert';
import {
  USER_ROLES,
  normalizeRole,
  getDefaultRouteForRole,
  isRouteAllowedForRole,
  toSafeString,
  getDisplayName,
  getUserInitials,
  normalizeUser,
  normalizeNotificationTargetUrl
} from './userUtils.js';

console.log('Running userUtils unit tests...');

// 1. normalizeRole tests
assert.strictEqual(normalizeRole("Student"), USER_ROLES.STUDENT);
assert.strictEqual(normalizeRole("instructor"), USER_ROLES.INSTRUCTOR);
assert.strictEqual(normalizeRole("  ADMIN  "), USER_ROLES.ADMIN);
assert.strictEqual(normalizeRole(["Instructor"]), USER_ROLES.INSTRUCTOR);
assert.strictEqual(normalizeRole("InvalidRole"), "");
assert.strictEqual(normalizeRole(null), "");
assert.strictEqual(normalizeRole(undefined), "");
assert.strictEqual(normalizeRole(123), "");

// 2. Route helper tests
assert.strictEqual(getDefaultRouteForRole(USER_ROLES.STUDENT), "/");
assert.strictEqual(getDefaultRouteForRole(USER_ROLES.INSTRUCTOR), "/instructor");
assert.strictEqual(getDefaultRouteForRole(USER_ROLES.ADMIN), "/admin");

assert.strictEqual(isRouteAllowedForRole("/cart", USER_ROLES.STUDENT), true);
assert.strictEqual(isRouteAllowedForRole("/cart", USER_ROLES.INSTRUCTOR), false);
assert.strictEqual(isRouteAllowedForRole("/instructor", USER_ROLES.INSTRUCTOR), true);
assert.strictEqual(isRouteAllowedForRole("/instructor", USER_ROLES.STUDENT), false);
assert.strictEqual(isRouteAllowedForRole("/admin", USER_ROLES.ADMIN), true);
assert.strictEqual(isRouteAllowedForRole("/admin", USER_ROLES.INSTRUCTOR), false);

// 3. toSafeString tests
assert.strictEqual(toSafeString("  Nguyen Van A  "), "Nguyen Van A");
assert.strictEqual(toSafeString(""), "");
assert.strictEqual(toSafeString(null), "");
assert.strictEqual(toSafeString(undefined), "");
assert.strictEqual(toSafeString(123), "123");
assert.strictEqual(toSafeString(true), "true");
assert.strictEqual(toSafeString({ fullName: "Nguyen" }, "Fallback"), "Fallback");

// 4. getDisplayName tests
assert.strictEqual(getDisplayName({ fullName: "Nguyen Van A" }), "Nguyen Van A");
assert.strictEqual(getDisplayName({ displayName: "A Nguyen" }), "A Nguyen");
assert.strictEqual(getDisplayName({ email: "test@edumy.com" }), "test@edumy.com");
assert.strictEqual(getDisplayName({ profile: { fullName: "Nested Name" } }), "Nested Name");
assert.strictEqual(getDisplayName(null), "User");
assert.strictEqual(getDisplayName({ fullName: { invalid: "object" } }), "User");
assert.strictEqual(getDisplayName({ fullName: 12345 }), "12345");

// 5. getUserInitials tests
assert.strictEqual(getUserInitials({ fullName: "Nguyen Van A" }), "NA");
assert.strictEqual(getUserInitials({ fullName: "SingleWord" }), "S");
assert.strictEqual(getUserInitials({ fullName: "" }), "U");
assert.strictEqual(getUserInitials(null), "U");
assert.strictEqual(getUserInitials({ fullName: { invalid: "object" } }), "U");

// 6. normalizeUser tests
const user1 = normalizeUser({ userId: 1, fullName: "  Nguyen Van A  ", email: "a@test.com", avatarUrl: "/avatar.png", role: "Student" });
assert.deepStrictEqual(user1, { id: 1, userId: 1, fullName: "Nguyen Van A", email: "a@test.com", avatarUrl: "/avatar.png", role: "Student" });

const user2 = normalizeUser({ data: { user: { id: 2, displayName: "Instructor B", email: "b@test.com", avatarUrl: null, role: "Instructor" } } });
assert.deepStrictEqual(user2, { id: 2, userId: 2, fullName: "Instructor B", email: "b@test.com", avatarUrl: "", role: "Instructor" });

const user3 = normalizeUser({ id: 3, fullName: { bad: "data" }, email: "c@test.com", role: "Unknown" });
assert.deepStrictEqual(user3, { id: 3, userId: 3, fullName: "", email: "c@test.com", avatarUrl: "", role: "" });

assert.strictEqual(normalizeUser(null), null);
assert.strictEqual(normalizeUser("invalid"), null);
assert.strictEqual(normalizeUser([]), null);

// 7. normalizeNotificationTargetUrl tests
assert.strictEqual(normalizeNotificationTargetUrl("courses/4#review-18"), "/courses/4#review-18");
assert.strictEqual(normalizeNotificationTargetUrl("/my-courses/68/learn?discussion=82"), "/my-courses/68/learn?discussion=82");
assert.strictEqual(normalizeNotificationTargetUrl("javascript:alert(1)"), "");
assert.strictEqual(normalizeNotificationTargetUrl("https://malicious-site.com"), "");
assert.strictEqual(normalizeNotificationTargetUrl(null), "");

console.log('✅ All userUtils unit tests passed successfully!');
