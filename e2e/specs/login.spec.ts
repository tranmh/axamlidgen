import { loginPage } from "../pages/LoginPage.js";

describe("Login flow", () => {
  it("submits the login form using AutomationIds from the generated manifest", async () => {
    await loginPage.signIn("user@example.com", "secret");
    // In a live run this would assert that the error banner is hidden
    // and the user has navigated forward. For this PoC the test is
    // primarily a structural demonstration of consuming the manifest.
    await expect(loginPage.errorBanner).not.toBeDisplayed();
  });

  it("shows the cancel button", async () => {
    await expect(loginPage.cancelButton).toBeDisplayed();
  });
});
