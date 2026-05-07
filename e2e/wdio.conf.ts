/**
 * WebdriverIO config targeting a WPF desktop application via Appium 2 + appium-windows-driver.
 *
 * NOTE: This suite executes only on Windows. The `appium:app` path below points at the
 * built WPF executable; until a real WPF app is shipped, the suite is structurally
 * complete but cannot run live. Validate the suite locally with `npm run typecheck`.
 */
export const config: WebdriverIO.Config = {
  runner: "local",
  specs: ["./specs/**/*.spec.ts"],
  maxInstances: 1,
  capabilities: [
    {
      platformName: "Windows",
      "appium:automationName": "Windows",
      "appium:app": process.env.WPF_APP_PATH ?? "C:/path/to/your/SampleApp.exe",
      "appium:newCommandTimeout": 60,
    } as WebdriverIO.Capabilities,
  ],
  logLevel: "info",
  bail: 0,
  baseUrl: "",
  waitforTimeout: 10_000,
  connectionRetryTimeout: 120_000,
  connectionRetryCount: 3,
  hostname: "127.0.0.1",
  port: 4723,
  path: "/",
  framework: "mocha",
  reporters: ["spec"],
  mochaOpts: {
    ui: "bdd",
    timeout: 60_000,
  },
};
