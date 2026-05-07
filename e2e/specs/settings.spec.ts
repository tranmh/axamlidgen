import { settingsPage } from "../pages/SettingsPage.js";

describe("Settings page", () => {
  it("toggles dark mode and saves", async () => {
    await settingsPage.darkModeToggle.click();
    await settingsPage.saveButton.click();
  });
});
