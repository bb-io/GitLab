# Blackbird.io GitLab

Blackbird is the new automation backbone for the language technology industry. Blackbird provides enterprise-scale automation and orchestration with a simple no-code/low-code platform. Blackbird enables ambitious organizations to identify, vet and automate as many processes as possible. Not just localization workflows, but any business and IT process. This repository represents an application that is deployable on Blackbird and usable inside the workflow editor.

## Introduction

<!-- begin docs -->

GitLab is a developer platform that allows developers to create, store, and manage their code. This app focuses on connecting repository events and file actions into the Blackbird ecosystem.

## Before setting up

Before you can connect you need to make sure that:

- You have a GitLab account.
- You have the access level required by the feature: Reporter for cross-repository polling, or Maintainer/Owner for project webhook events.

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

### Repository

- **Create new repository** Create repository with selected settings.
  Advanced settings:
  - **User ID**: User ID that owns the created repository.
  - **Default branch**: Default branch name for the created repository.
  - **Namespace ID**: Namespace ID for the created repository.
  - **Description**: Repository description.
  - **Enable issues**: Option to enable issues.
  - **Enable merge requests**: Option to enable merge requests.
  - **Enable jobs**: Option to enable jobs.
  - **Enable wiki**: Option to enable wiki.
  - **Enable snippets**: Option to enable snippets.
  - **Enable container registry**: Option to enable the container registry.
  - **Enable shared runners**: Option to enable shared runners.
  - **Visibility**: Repository visibility.
  - **Import URL**: URL to import repository content from.
  - **Public jobs**: Option to make jobs public.
  - **Only allow merge if pipeline succeeds**: Option to allow merges only after a successful pipeline.
  - **Only allow merge if all discussions are resolved**: Option to allow merges only after all discussions are resolved.
  - **Enable LFS**: Option to enable large file storage.
  - **Enable request access**: Option to allow users to request access.
  - **Tags**: Multiple tags to assign to the repository.
  - **Enable printing merge request link**: Option to print merge request links.
  - **CI config path**: CI config file path.
  - **Initialize with README**: Option to create an initial README file.
- **Download file** Download a file from a repository by file path.
  Advanced settings:
  - **Branch name**: Branch to use instead of the default branch.
- **Get all files in folder** Get files from a repository folder.
  Advanced settings:
  - **Folder path (e.g. "Folder1/Folder2")**: Folder path to get files from.
  - **Include subfolders**: Option to include files in nested folders.
- **Get repository** Get repository details.
- **Search repository issues** Get open issues in a repository.
- **Search repository merge requests** Get open merge requests in a repository.
- **Search repository folder content** Search folder content in a repository.
  Advanced settings:
  - **Content type**: Content type to include.
- **Search repositories** Search repositories available to connection.
- **Search files by filepaths** Get files from a repository by file paths.
- **Check if branch exists** Check whether branch exists in a repository.

### Commit

- **Search commits** Search commits in a repository.
  Advanced settings:
  - **Authors to include**: Multiple author names or emails to include.
  - **Authors to exclude**: Multiple author names or emails to exclude.
  - **Commit after**: Only commits after this date.
  - **Commit before**: Only commits before this date.
  - **Commit message contains**: Text that commit message must contain.
  - **File path**: Only commits touching this file path.
- **Find commit** Find first commit that matches search filters in a repository.
- **Get commit** Get commit details by commit ID.
- **List added or modified files in X hours** Search files added or modified during specified number of hours.
  Advanced settings:
  - **Path pattern**: Use forward slash '/' to represent directory separator. Use '*' to represent wildcards in file and directory names. Use '**' to represent arbitrary directory depth.
- **Upload file** Create file or update existing file in a repository.
- **Update file** Update existing file in a repository.
- **Delete file** Delete file from a repository.

### Merge request

- **Search merge requests** Search merge requests in a repository.
- **Get merge request** Get merge request details.
- **Create merge request** Create merge request.
- **Complete merge request** Complete merge request by merging it.
  Advanced settings:
  - **Merge commit message**: Text for the merge commit message.

### Branch

- **Search branches** Search repository branches.
- **Get branch** Get branch details by name.
- **Create branch** Create branch from a base branch.

## Events

### Pushes

- **On commit pushed** On commit pushed to a repository branch.
- **On files added** On files added by new commits. Outputs paths to added files.
- **On files modified** On files modified by new commits. Outputs paths to modified files.
- **On files added or modified** On files added or modified by new commits. Outputs paths to added or modified files.
- **On files removed** On files removed by new commits. Outputs paths to removed files.

For the file specific events, a path parameter can be specified in order to narrow down the event to only files in specific folders or files that have certain extensions. Use the forward slash '/' to represent directory separator. Use '\*' to represent wildcards in file and directory names. Use '\*\*' to represent arbitrary directory depth.

For example: when you want to create an event that triggers only when .html files are modified in a folder called _locales_. Then the path pattern should be: _/locales/\*.html_

![1705407685118](image/README/1705407685118.png)

### Polling for modified files across repositories

- **On files modified across repositories** periodically checks push activity in multiple selected repositories. It uses read-only GitLab API requests and does not create project or group webhooks.

#### Permissions

Connection user needs at least the Reporter role in every selected repository. For a personal access token, grant `read_user` to read the global event feed and `read_api` to read projects, comparisons, and commit diffs. Existing tokens with the broader `api` scope also work.

#### Inputs and matching

**Repository IDs to include** is required. It is a multi-select field that displays each repository as `namespace/repository` while storing its stable numeric GitLab project ID.

All other inputs are optional:

- **Branches to watch**: case-sensitive regular expressions. A branch must match at least one when configured.
- **Branches to ignore**: case-sensitive regular expressions. Ignore matches take precedence over watch matches.
- **File patterns to watch**: glob patterns matched only against modified files. A bare filename such as `en.po` matches at any directory depth as `**/en.po`. Added, deleted, and renamed files do not qualify. Leaving this input empty accepts any modified file.
- **Commit messages to include**: case-sensitive regular expressions. A commit must match at least one when configured.
- **Commit messages to exclude**: case-sensitive regular expressions. Exclude matches take precedence over include matches.
- **Pusher user IDs to watch**: GitLab user IDs. A push must come from one of these users when configured.
- **Pusher user IDs to ignore**: GitLab user IDs. Ignore matches take precedence over watch matches.

Commit-message and file-pattern matches must occur in the same commit. One qualifying commit makes the push qualify, but output includes every commit from that push. Matched file paths come only from qualifying commits.

#### Output and polling behavior

Each flight returns one `Pushes` batch containing every matching push found during that poll. Each push contains repository ID, repository name with namespace, branch name, push time, pusher user ID, matched file paths, and commits with hash, message, author name, and author email.

First poll establishes a baseline and does not trigger. Changing any input also establishes a new baseline, preventing activity selected under the new configuration from being replayed. Later polls rescan a 24-hour overlap and de-duplicate processed GitLab event IDs. Memory advances only after the complete scan and all enrichment requests succeed, so an API failure or safety-limit failure does not lose events.

#### API usage and safeguards

A quiet poll reads one or more pages from the global GitLab Events API, then filters exact repository IDs before making repository-specific requests. A candidate push needs one compare request, one paginated diff request per message-qualified commit, and one project lookup per emitted repository. Enrichment runs with at most four concurrent requests. HTTP 429 responses are retried using `Retry-After` when supplied, capped at 60 seconds per retry; otherwise bounded exponential backoff with jitter is used.

Polling has a 200-page event-feed limit, a 100-page limit per commit diff, and a five-minute total scan limit. Reaching a limit fails the poll instead of returning partial output or advancing memory.

GitLab cannot provide a reliable comparison range for every push:

- Bulk pushes above GitLab's multi-reference threshold, normally three refs, can omit branch and commit-range details. Incomplete events are recorded as processed and skipped.
- New branches, deleted branches, and force pushes with an unrecoverable comparison range are skipped.
- Diffs flagged by GitLab as collapsed or too large are skipped because matched paths may be incomplete.
- GitLab can stop very large commit diffs at the instance file limit; older versions might not flag that truncation, so matching can be less complete for unusually large commits.
- The 24-hour overlap handles ordinary delays and pagination movement, not events that become visible more than 24 hours late.
- The global event feed can return metadata for every project visible to the connection before local repository filtering occurs.

## Example

The following bird creates a new memoQ project whenever files are updated or added to a specific folder in a GitLab repository.

![1705408481146](image/README/1705408481146.png)

## Feedback

Do you want to use this app or do you have feedback on our implementation? Reach out to us using the [established channels](https://www.blackbird.io/) or create an issue.

<!-- end docs -->
