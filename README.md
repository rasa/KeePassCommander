[![Github All Releases](https://img.shields.io/github/downloads/MircoBabin/KeePassCommander/total)](https://github.com/MircoBabin/KeePassCommander/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/MircoBabin/KeePassCommander/blob/master/LICENSE.md)

# KeePass Commander
KeePass Commander is a plugin for the [KeePass password store](https://keepass.info/ "KeePass"). 
It's purpose is to provide a communication channel for php-scripts, bat-files, powershell, python, C#, git, ... to be able to query the KeePass password store from the commandline without configuration and without password.

![Screenshot](screenshot.png)

# Download binary
For Windows (.NET framework 4), [the latest version can be found here](https://github.com/MircoBabin/KeePassCommander/releases/latest "Latest Version").
The plugin works with KeePass 2.44. Because the plugin barely uses anything from KeePass, it will probably work with all future KeePass versions.

Download the zip and unpack it in the KeePass directory where KeePass.exe is located.

The minimum .NET framework required is 4.0.
.NET framework version 3.5 is the first framework to implement the System.IO.Pipes namespace, but KeePass only supports v4.0 and v2.0.50727.

*For unattended automatic installation scripts, read the section "Automatic installation scripts" lower down the page.*

# Help

Execute **KeePassCommand.exe** without parameters to view the help.

```
KeePassCommand 2.7
https://github.com/MircoBabin/KeePassCommander - MIT license

KeePass Commander is a plugin for the KeePass password store (https://keepass.info/).
It's purpose is to provide a communication channel for php-scripts, bat-files, powershell, python, C#, ... to be able to query the KeePass password store from the commandline without configuration and without password.

Syntax: KeePassCommand.exe <command> {-out:outputfilename OR -out-utf8:outputfilename} ...
- Unless -out or -out-utf8 is used, output will be at the console (STDOUT).
- When -out-utf8:outputfile is used, output will be written in outputfile using UTF-8 codepage.
- When -out:outputfile is used, output will be written in outputfile using ANSI codepage.
- "KeePass-entry-title" must exactly match (case sensitive), there is no fuzzy logic. All open databases in KeePass are searched.
- When the expected "KeePass-entry-title" is not found (you know it must be there), you can assume KeePass is not started or the required database is not opened.

* Basic get
KeePassCommand.exe get "KeePass-entry-title" "KeePass-entry-title" ...
e.g. KeePassCommand.exe get "Sample Entry"
- "Notes" are outputted as UTF-8, base64 encoded.

* Advanced get string field
KeePassCommand.exe getfield "KeePass-entry-title" "fieldname" "fieldname" ...
e.g. KeePassCommand.exe getfield "Sample Entry" "extra field 1" "extra password 1"

* Advanced get file attachment
KeePassCommand.exe getattachment "KeePass-entry-title" "attachmentname" "attachmentname" ...
e.g. KeePassCommand.exe getattachment "Sample Entry" "example_attachment.txt"
- Attachment is outputted as binary, base64 encoded.

* Advanced get file attachment raw into file
KeePassCommand.exe getattachmentraw -out:outputfilename "KeePass-entry-title" "attachmentname"
e.g. KeePassCommand.exe getattachmentraw -out:myfile.txt "Sample Entry" "example_attachment.txt"
- Attachment is saved in outputfilename, outputted as binary.

* Advanced get notes
KeePassCommand.exe getnote "KeePass-entry-title" "KeePass-entry-title" ...
e.g. KeePassCommand.exe getnote "Sample Entry"
- "Notes" are outputted as UTF-8, base64 encoded.

* Advanced get notes into file
KeePassCommand.exe getnoteraw <-out-utf8:outputfile or -out:> "KeePass-entry-title"
e.g. KeePassCommand.exe getnoteraw -out-utf8:mynote.txt "Sample Entry"
- With -out-utf8, "Notes" are outputted as UTF-8.
- With -out, "Notes" are outputted in ANSI codepage.

```

# Examples

Examples are found in the github directory **example**.

- example.kdbx is a KeePass database. It's master password is **example**.
- [FromPhp.php](example/FromPhp.php) can be used to query the KeePass password store from PHP. With minimal modifications you can use it anywhere.
- [FromBat.bat](example/FromBat.bat) can be used to query the KeePass password store from a BAT file. With minimal modifications you can use it anywhere.
- [FromPowershell.ps1](example/FromPowershell.ps1) can be used to query the KeePass password store from PowerShell. With minimal modifications you can use it anywhere.
- [FromPython.py](example/FromPython.py) can be used to query the KeePass password store from Python. With minimal modifications you can use it anywhere.
- [CsharpExample](example/CsharpExample/src/CsharpExample/Program.cs) can be used to query the KeePass password store from C#. With minimal modifications you can use it anywhere.

# GIT

Using [Git Credentials via KeePassCommander](https://github.com/MircoBabin/GitCredentialsViaKeePassCommander) the credentials for git repositories can be queried from KeePass upon a pull or push command.

# Why
The plugin [KeePassHttp](https://github.com/pfn/keepasshttp/) already exists for querying the password store. 
I did not want to use this plugin, because it embeds a http server inside KeePass. 
And I don't want to "pair" with a code, because I want to communicate from the commandline, without configuration.

So I build KeePassCommander.dll plugin which runs a Windows named-pipe-server inside KeePass. And a KeePassCommand.exe commandline tool to communicate with KeePassCommander.dll. 

I'm using this plugin among other things to automate DeployHQ. In KeePass I store the DeployHQ API key. From a php script the API key is queried and then used. 

# Automatic installation scripts
For unattended installation scripts the following flow can be used for the latest version:

1) Download https://github.com/MircoBabin/KeePassCommander/releases/latest/download/release.download.zip.url-location
2) Read the text of this file into **latest-download-url**. The file only contains an url, so the encoding is ASCII. *The encoding UTF-8 may also be used to read the file, because ASCII is UTF-8 encoding.*
3) Download the zip from the **latest-download-url** to local file **KeePassCommander.zip**. *Each release carries the version number in the filename. To prevent not knowing the downloaded filename, download to a fixed local filename.*
4) Unpack the downloaded **KeePassCommander.zip** in the KeePass directory where KeePass.exe is located.

# Debug

Via KeePass commandline options this plugin can write logging. Use --KeePassCommanderDebug=*full-path-to-filename* to specify the filename.

```
KeePass.exe --debug --KeePassCommanderDebug=c:\incoming\KeePassCommander.log
```

# Contributions
Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md "contributing") before making any contribution!

# License
[The license is MIT.](LICENSE.md "license")





