# Blackbird.io GitLab

Blackbird is the new automation backbone for the language technology industry. Blackbird provides enterprise-scale automation and orchestration with a simple no-code/low-code platform. Blackbird enables ambitious organizations to identify, vet and automate as many processes as possible. Not just localization workflows, but any business and IT process. This repository represents an application that is deployable on Blackbird and usable inside the workflow editor.

## Introduction

<!-- begin docs -->

GitLab, is a developer platform that allows developers to create, store, and manage their code. This GitLab app focusses on connecting GitLab events and file actions into the Blackbird ecosystem.

## Before setting up

Before you can connect you need to make sure that:

- You have a GitLab account.
- You have owner permissions on the repository you want to automate on.

## Connecting

GitLab supports the following connection types in Blackbird:

- **OAuth**: use this option for GitLab-hosted accounts.
- **OAuth Self-managed**: use this option for self-managed GitLab instances hosted on a custom domain.
- **Personal Access Token**: use this option when you want to connect with a personal token instead of OAuth.

1. Navigate to apps and search for GitLab. If you cannot find GitLab then click _Add App_ in the top right corner, select GitLab and add the app to your Blackbird environment.
2. Click _Add Connection_.
3. Name your connection for future reference e.g. 'My GitLab connection'.
4. Choose the connection type that matches your GitLab environment.
5. Complete the authentication flow for the selected connection type.
6. When you return to Blackbird, confirm that the connection has appeared and the status is _Connected_.

![1705402013379](image/README/1705402013379.png)

### Connecting with OAuth

1. Create a new GitLab connection in Blackbird.
2. Select **OAuth** as the connection type.
3. Click **Authorize connection**.
4. Follow the GitLab authorization flow and approve the requested access.
5. Return to Blackbird and confirm that the connection status is _Connected_.

### Connecting to self-managed GitLab

If your GitLab instance is hosted on a custom domain, use the **OAuth Self-managed** connection type in Blackbird.

1. In your self-managed GitLab instance, navigate to **Admin area** -> **Applications** and click **Add new application**.
2. Enter an application name, for example _Blackbird Self-Managed Test_.
3. Set **Redirect URI** to `https://bridge.blackbird.io/api/AuthorizationCode`.
4. Save the application and copy the generated **Application ID** and **Secret**.
5. In Blackbird, create a new GitLab connection and choose **OAuth Self-managed** as the connection type.
6. Enter your GitLab instance URL as **Base URL**, for example `https://yourdomain.example.com`.
7. Paste the GitLab **Application ID** into **Client ID** and the GitLab **Secret** into **Client secret**.
8. Click **Authorize connection** and complete the OAuth consent flow in your self-managed GitLab instance.
9. Return to Blackbird and confirm that the connection has appeared and the status is _Connected_.

### Connecting with a personal access token

1. Create a personal access token with the scopes required for the actions you want to use.
2. In Blackbird, create a new GitLab connection and choose **Personal Access Token** as the connection type.
3. Paste the token into the **API key** field.
4. Save the connection and confirm that the status is _Connected_.

## Actions

### Repositories

- **List repositories**
- **Create new repository**
- **Get repository**
- **Get repository issues**
- **List repository folder content**

### Branches

- **List branches**
- **Get branch**

### Commits

- **Search commits**
- **Find commit**
- **Get commit**

### Merge requests

- **Create merge request**
- **List merge requests**
- **Get merge request**
- **Get repository merge requests**
- **Complete merge request**

### Files

- **Get repository file**
- **Get all files in folder**
- **Get files by filepaths**
- **Delete file**
- **Push file**
- **Update file**

### Users

- **Get my user data**
- **Get user**
- **Get user by username**

### Utility

- **Is file in folder**

## Events

### Pulls

= **On pull request action** occurs when there is activity on a pull request. See [this page](https://docs.GitLab.com/en/webhooks/webhook-events-and-payloads#pull_request) for more info.

### Pushes

- **On commit pushed** occurs when there is a push to a repository branch. This includes when a commit is pushed, when a commit tag is pushed, when a branch is deleted, when a tag is deleted, or when a repository is created from a template.
- **On files added** is triggered when new commits have new files. It returns the paths to all added files.
- **On files modified** is triggered when new commits modify files. It returns paths to all modified files.
- **On files added or modified** is triggered when new commits add new files or modify files. It returns paths to all these files.
- **On files removed** is triggered when new commits remove files. It returns paths to all deleted files.

For the file specific events, a path parameter can be specified in order to narrow down the event to only files in specific folders or files that have certain extensions. Use the forward slash '/' to represent directory separator. Use '\*' to represent wildcards in file and directory names. Use '\*\*' to represent arbitrary directory depth.

For example: when you want to create an event that triggers only when .html files are modified in a folder called _locales_. Then the path of the **On files added or modified** event should be: _/locales/\*.html_

![1705407685118](image/README/1705407685118.png)

## Example

The following bird creates a new memoQ project whenever files are updated or added to a specific folder in a GitLab repository.

![1705408481146](image/README/1705408481146.png)

## Feedback

Do you want to use this app or do you have feedback on our implementation? Reach out to us using the [established channels](https://www.blackbird.io/) or create an issue.

<!-- end docs -->
