import { AutomationIds } from "../automation-ids.generated.js";

export class SettingsPage {
  get darkModeToggle() {
    return $(`~${AutomationIds.SettingsView_ToggleSwitch_DarkMode}`);
  }

  get emailNotificationsToggle() {
    return $(`~${AutomationIds.SettingsView_ToggleSwitch_EmailNotifications}`);
  }

  get saveButton() {
    return $(`~${AutomationIds.SettingsView_Button_Save}`);
  }
}

export const settingsPage = new SettingsPage();
