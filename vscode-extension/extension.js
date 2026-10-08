// KeyShot VS Code companion. Talks to the desktop app over its local named pipe;
// the app itself does all keyboard and audio work, globally, outside VS Code.
const vscode = require('vscode');
const net = require('net');
const path = require('path');
const fs = require('fs');
const { spawn } = require('child_process');

const PIPE = '\\\\.\\pipe\\KeyShot.Control';

/** Send one command line; resolves to the reply line, or null when KeyShot is not running. */
async function send(command, timeoutMs = 1000) {
  // The app serves one client at a time and re-opens the pipe between clients, so a
  // connect can land in that ~ms gap. Retry briefly before concluding it's not running.
  for (let attempt = 0; attempt < 25; attempt++) {
    const reply = await sendOnce(command, timeoutMs);
    if (reply !== undefined) return reply;
    await new Promise((r) => setTimeout(r, 20 + Math.random() * 30));
  }
  return null;
}

/** One attempt: reply string, null on timeout/empty reply, undefined if the pipe was unavailable. */
function sendOnce(command, timeoutMs) {
  return new Promise((resolve) => {
    let reply = '';
    let done = false;
    const finish = (value) => {
      if (done) return;
      done = true;
      socket.destroy();
      resolve(value);
    };
    const socket = net.connect(PIPE, () => socket.write(command + '\n'));
    socket.setEncoding('utf8');
    socket.on('data', (chunk) => {
      reply += chunk;
      if (reply.includes('\n')) finish(reply.trim());
    });
    socket.on('error', (err) => finish(['ENOENT', 'EBUSY', 'EPIPE'].includes(err.code) && !reply ? undefined : null));
    socket.on('end', () => finish(reply.trim() || null));
    socket.setTimeout(timeoutMs, () => finish(null));
  });
}

function exePath() {
  const configured = vscode.workspace.getConfiguration('keyshot').get('executablePath');
  if (configured) return configured;
  return path.join(process.env.LOCALAPPDATA || '', 'Programs', 'KeyShot', 'KeyShot.exe');
}

function launch(args = []) {
  const exe = exePath();
  if (!fs.existsSync(exe)) {
    vscode.window.showErrorMessage(`KeyShot not found at ${exe}. Install it with scripts/install.ps1 or set "keyshot.executablePath".`);
    return false;
  }
  spawn(exe, args, { detached: true, stdio: 'ignore' }).unref();
  return true;
}

function activate(context) {
  const item = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
  item.command = 'keyshot.toggle';
  item.show();
  context.subscriptions.push(item);

  const render = (state) => {
    if (state === 'ACTIVE') {
      item.text = '$(target) KeyShot';
      item.tooltip = 'KeyShot is ACTIVE — click to disable';
      item.backgroundColor = undefined;
    } else if (state === 'INACTIVE') {
      item.text = '$(circle-slash) KeyShot';
      item.tooltip = 'KeyShot is INACTIVE — click to enable';
      item.backgroundColor = undefined;
    } else {
      item.text = '$(debug-disconnect) KeyShot';
      item.tooltip = 'KeyShot is not running — click to launch';
      item.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
    }
  };

  const refresh = async () => render(await send('STATUS'));

  const run = (command) => async () => {
    const reply = await send(command);
    if (reply === null) {
      const choice = await vscode.window.showWarningMessage('KeyShot is not running.', 'Launch KeyShot');
      if (choice) await launchAndApply(command);
      return;
    }
    render(reply);
  };

  const launchAndApply = async (command) => {
    if (!launch(command === 'DISABLE' ? ['--disable', '--minimized'] : ['--enable', '--minimized'])) return;
    setTimeout(refresh, 1500);
  };

  context.subscriptions.push(
    vscode.commands.registerCommand('keyshot.enable', run('ENABLE')),
    vscode.commands.registerCommand('keyshot.disable', run('DISABLE')),
    vscode.commands.registerCommand('keyshot.showSettings', run('SHOW')),
    vscode.commands.registerCommand('keyshot.toggle', async () => {
      const reply = await send('TOGGLE');
      if (reply === null) return launchAndApply('ENABLE');
      render(reply);
    }),
    vscode.commands.registerCommand('keyshot.launch', async () => {
      if ((await send('PING')) !== null) return send('SHOW');
      launch();
      setTimeout(refresh, 1500);
    }),
  );

  const config = vscode.workspace.getConfiguration('keyshot');
  const seconds = Math.max(1, config.get('pollIntervalSeconds') || 3);
  const timer = setInterval(refresh, seconds * 1000);
  context.subscriptions.push({ dispose: () => clearInterval(timer) });

  refresh().then(async () => {
    if (config.get('launchOnStartup') && (await send('PING')) === null) {
      launch(['--minimized']);
      setTimeout(refresh, 1500);
    }
  });
}

function deactivate() {}

module.exports = { activate, deactivate, send };
