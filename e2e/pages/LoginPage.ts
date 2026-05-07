import { AutomationIds } from "../automation-ids.generated.js";

/**
 * Page object for SampleApp's LoginView.
 * Every locator references a generated AutomationId constant — referencing
 * an unknown constant becomes a TypeScript compile error.
 */
export class LoginPage {
  get emailField() {
    return $(`~${AutomationIds.LoginView_Email_Field}`);
  }

  get passwordField() {
    return $(`~${AutomationIds.LoginView_Password_Field}`);
  }

  get submitButton() {
    return $(`~${AutomationIds.LoginView_Button_Submit}`);
  }

  get cancelButton() {
    return $(`~${AutomationIds.LoginView_Button_Cancel}`);
  }

  get errorBanner() {
    return $(`~${AutomationIds.LoginView_Error_Banner}`);
  }

  async signIn(email: string, password: string): Promise<void> {
    await this.emailField.setValue(email);
    await this.passwordField.setValue(password);
    await this.submitButton.click();
  }
}

export const loginPage = new LoginPage();
