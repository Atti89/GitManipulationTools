using System.Collections;
using System.Collections.Generic;
using System.IO;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.Net;
using LitJson;
using System.Threading.Tasks;

namespace GitTest
{

    /* Development-plan:
     * 
     * Milestone 1:
     *  Backend - .git-folders:
     *      DONE - method to find any of them on the choosen medium
     *      DONE - method to create a repository
     *      DONE - method to clone an existing online repository
     *      DONE - method to create the city of the repository
     *      DONE - method to create a commit
     *              => catch empty commit exception
     *      DONE - method to pull from a repository
     *      DONE - method to push to a repositroy
     *      DONE - method that identifies the different types of git files
     *      DONE - method to create branch
     *              => catch exceptions
     *      - method to delete branch
     *      - method to change between branches
     *      - method to add files(s) to .gitignore
     *      - method that handles merging
     *      - (optional) method to delete repositories
     * 
     * Milestone 2: 
     *  Frontend
     *      - CityScript upgrade
     *      
     * 
     * Milestone 3: - ?
     *  
     */

    

    class Program
    {

        public static string GITHUB_URL = "https://github.com/";
        public static string GITHUB_API = "https://api.github.com/";

        private static Repository _repository;
        private static string _repositoryPath;
        public static Repository Repository
        {
            get
            {
                return _repository;
            }

            set
            {
                if (value.Equals(_repository)) return;

                _repository = value;

                int index = _repository.Info.WorkingDirectory.LastIndexOf('\\');
                _repositoryPath = _repository.Info.WorkingDirectory.Remove(index);

                Status = _repository.RetrieveStatus();

                Console.WriteLine(Status);

                Branches = _repository.Branches;

                ActualBranch = _repository.Head;
            }
        }

        public static RepositoryStatus Status { get; private set; }

        public static Branch ActualBranch { get; private set; }

        public static BranchCollection Branches { get; private set; }


        static void Main(string[] args)
        {
            CreateRepository("C://Users//Attila//Desktop//GitTestProj", "GitManipulationTools", 
                             "Atti89", "8i4e76a6", "kettattila@hotmail.com").Wait();

            

            Console.ReadLine();
        }

        public static FileStatus GetGitFileType(string filePath)
        {

            if (!filePath.Contains(_repositoryPath) ||
                 filePath.Equals(_repositoryPath) ||
                 filePath.Contains(".git"))
                return FileStatus.Nonexistent;

            string newPath = filePath.Replace(_repositoryPath + "\\", "");
            return Repository.RetrieveStatus(filePath);
        }

        //public delegate void OnRepositoryCreated(Repository repository);
        public static async Task<Repository> CreateRepository(string path, string repoName = null,
                                                   string username = null, string password = null, string email = null,
                                                   bool onlyLocal = false)
        {

            Repository repo;
            // check if the directory exists, create if not
            DirectoryInfo dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                LogInfos("Creating new Folder:\n" + path);
                dir.Create();
            }

            if (onlyLocal)
            {
                // return, if there is already a repository
                if (Repository.IsValid(path))
                {
                    LogInfos(path + "\nis already a repository.");
                    return null;
                    //yield break;
                }

                // create repository local
                Repository.Init(path);
                LogInfos("Repository succesful created at\n" + path);
                repo = new Repository(path);
                return repo;

            }
            else
            {
                if (username == null || password == null ||
                    email == null || repoName == null)
                {
                    LogInfos("You try to create a remote repository.\n" +
                             "Username, password, email and the\n" +
                             "name of the Repository must be defined.");
                    //yield break;
                    return null;
                }

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.github.com/user/repos");
                request.Method = "POST";
                request.Headers.Add("Content-Type", "application/json");
                request.Headers.Add("Authorization", CreateAuthorizationString(username, password));

                //UnityWebRequest request = new UnityWebRequest("https://api.github.com/user/repos", "POST");
                //request.SetRequestHeader("Content-Type", "application/json");
                //request.SetRequestHeader("Authorization", CreateAuthorizationString(username, password));

                string jsonString = @"
                      {""name"": """ + repoName + "\"," +
                      "\"auto_init\": " + true.ToString().ToLower() + "," +
                      "\"private\": " + false.ToString().ToLower() + "," +
                      "\"gitignore_template\": \"nanoc\"}";

                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(jsonString);
                //request.uploadHandler = new UploadHandlerRaw(bytes);
                //request.downloadHandler = new DownloadHandlerBuffer();
                request.GetRequestStream().Write(bytes, 0, bytes.Length);

                //yield return request.SendWebRequest();
                await request.GetRequestStreamAsync();

                //Console.WriteLine(request.downloadHandler.text);
                string res = new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();
                Console.WriteLine(res);

                //JsonData data = JsonMapper.ToObject(request.downloadHandler.text);
                JsonData data = JsonMapper.ToObject(res);
                if (data.Keys.Contains("message"))
                {
                    if (data["message"].Equals("Bad credentials"))
                        LogInfos("Wrong username or password.");
                    else
                        LogInfos("A repository with the given name\n" +
                                 "exists already on your account.");
                    //yield break;
                    return null;
                }

                LogInfos("Repository " + repoName + " was succesful created\n" +
                         "and initialized on your github-account.");

                repo = CloneRepository(GITHUB_URL + username + "/" + repoName + ".git",
                                       path, username, password);

                return repo;

                
            }

        }

        public static void AddRemote(string repoPath, string url, string remoteName = null)
        {
            if (!Repository.IsValid(repoPath))
            {
                LogInfos(repoPath + "\nis not a valid repository.");
                return;
            }
            Repository repo = new Repository(repoPath);
            if (remoteName == null) remoteName = "origin";
            foreach (Remote remote in repo.Network.Remotes)
            {
                if (remote.Name.Equals(remoteName))
                {
                    LogInfos(remoteName + " is already in use at\n" + repoPath);
                    return;
                }
            }
            repo.Network.Remotes.Add(remoteName, url);
        }

        public static Repository CloneRepository(string url, string path, string username = null, string password = null)
        {
            try
            {
                // check if the directory exists, create if not
                DirectoryInfo dir = new DirectoryInfo(path);
                if (!dir.Exists)
                {
                    LogInfos("Creating new Folder:\n" + path);
                    dir.Create();
                }

                CloneOptions options = null;
                if (username != null && password != null)
                {
                    options = new CloneOptions();
                    options.CredentialsProvider = new CredentialsHandler(
                        (_url, _name, _types) => new UsernamePasswordCredentials
                        {
                            Username = username,
                            Password = password
                        });
                }
                Console.WriteLine(options);
                if (options == null) Repository.Clone(url, path);
                else Repository.Clone(url, path, options);
                LogInfos(url + "succesful cloned into\n" + path);
                return new Repository(path);
            }
            catch (Exception e)
            {
                string text = "Cloning from " + url + " failed.\n";
                if (e is LibGit2SharpException)
                    LogInfos(text + "Check your internet connection.");
                //else if (e is AuthenticationException)
                //    LogInfos(text + "Wrong username or password.");
                else
                    LogInfos(text + "An unknown error occurred.");
                return null;
            }
        }

        public static void Pull(string repoPath, string username, string email, string password = null)
        {
            if (!Repository.IsValid(repoPath))
            {
                LogInfos(repoPath + "\nis not a valid repository.");
                return;
            }
            Repository repo = new Repository(repoPath);
            RepositoryInformation info = repo.Info;
            try
            {
                PullOptions options = new PullOptions();
                if (password != null)
                {
                    FetchOptions fO = new FetchOptions();
                    fO.CredentialsProvider = new CredentialsHandler(
                        (url, user, types) => new UsernamePasswordCredentials
                        {
                            Username = username,
                            Password = password
                        });
                    options.FetchOptions = fO;
                }
                Commands.Pull(repo, new Signature(username, email, new DateTimeOffset(DateTime.Now)), options);
                LogInfos(info.Path + " succesful updated.");
            }
            catch (Exception e)
            {
                string text = "Update of " + info.Path + " failed.\n";
                if (e is LibGit2SharpException)
                    LogInfos(text + "Check your internet connection.");
                //else if (e is AuthenticationException)
                //    LogInfos(text + "Wrong username or password.");
                else
                    LogInfos(text + "An unkown error occured.");
            }

        }

        public static void Commit(string repoPath, string[] files, string username,
                                  string email, string commitMsg)
        {
            if (!Repository.IsValid(repoPath))
            {
                LogInfos(repoPath + "\nis not a valid repository.");
                return;
            }
            Repository repo = new Repository(repoPath);

            foreach (string file in files)
            {
                Commands.Stage(repo, file);
            }

            Signature author = new Signature(username, email, new DateTimeOffset(DateTime.Now));

            repo.Commit(commitMsg, author, author);

            LogInfos("Commit succesful in\n" + repoPath);
        }

        public static void Push(string repoPath, string branch, string username, string password)
        {
            if (!Repository.IsValid(repoPath))
            {
                LogInfos(repoPath + "\nis not a valid repository.");
                return;
            }
            Repository repo = new Repository(repoPath);
            RepositoryInformation info = repo.Info;
            try
            {
                PushOptions options = new PushOptions();
                options.CredentialsProvider = new CredentialsHandler(
                        (url, user, types) => new UsernamePasswordCredentials
                        {
                            Username = username,
                            Password = password
                        });
                repo.Network.Push(repo.Branches[branch], options);
                LogInfos("Push to branch " + branch + " succesful.");
            }
            catch (Exception e)
            {
                string text = "Push to branch " + branch + " failed.\n";
                if (e is LibGit2SharpException)
                    LogInfos(text + "Check your internet connection.");
                //else if (e is AuthenticationException)
                //    LogInfos(text + "Wrong username or password.");
                else
                    LogInfos(text + "An unkown error occured.");
            }
        }

        public static IEnumerator CreateBranch(string repoPath, string branchName, string username = null,
                                               string password = null, bool onlyLocal = false)
        {
            if (!Repository.IsValid(repoPath))
            {
                LogInfos(repoPath + "\nis not a valid repository.");
                yield break;
            }
            Repository repo = new Repository(repoPath);
            foreach (Branch b in repo.Branches)
            {
                if ((b.RemoteName != null && b.RemoteName.Equals(branchName)) ||
                    b.FriendlyName.Equals(branchName))
                {

                    LogInfos("A branch with the name " + branchName + "\nalready exists.");
                    yield break;
                }
            }
            Branch branch = repo.CreateBranch(branchName);

            if (username == null || password == null)
            {
                LogInfos("You try to create a remote repository.\n" +
                         "Username and password must be defined.");
                yield break;
            }

            string authorizationStr = CreateAuthorizationString(username, password);
            string apiRefsURL = GITHUB_API + "repos/" + username + "/" + GetRepositoryRemoteName(repo) + "/git/refs";
            Console.WriteLine(apiRefsURL);
            string masterBranchURL = apiRefsURL + "/heads/master";

            //UnityWebRequest request = new UnityWebRequest(masterBranchURL, "GET");
            //request.SetRequestHeader("Content-Type", "application/json");
            //request.SetRequestHeader("Authorization", authorizationStr);
            //request.downloadHandler = new DownloadHandlerBuffer();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(masterBranchURL);
            request.Method = "POST";
            request.Headers.Add("Content-Type", "application/json");
            request.Headers.Add("Authorization", authorizationStr);

            //yield return request.SendWebRequest();
            yield return request.GetResponse();

            string res = new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();
            Console.WriteLine(res);
            //JsonData data = JsonMapper.ToObject(request.downloadHandler.text);
            JsonData data = JsonMapper.ToObject(res);
            if (!data.Keys.Contains("object"))
            {
                LogInfos("Web request failed, check your internet connection.");
                yield break;
            }
            JsonData lastCommitInfos = data["object"];
            string sha = lastCommitInfos["sha"].ToString();

            //request = new UnityWebRequest(apiRefsURL, "POST");
            //request.SetRequestHeader("Content-Type", "application/json");
            //request.SetRequestHeader("Authorization", authorizationStr);
            request = (HttpWebRequest)WebRequest.Create(masterBranchURL);
            request.Method = "POST";
            request.Headers.Add("Content-Type", "application/json");
            request.Headers.Add("Authorization", authorizationStr);

            string jsonString = @"{""ref"": ""refs/heads/" + branchName + "\"," +
                                   "\"sha\": \"" + sha + "\"}";

            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(jsonString);
            //request.uploadHandler = new UploadHandlerRaw(bytes);
            //request.downloadHandler = new DownloadHandlerBuffer();
            request.GetRequestStream().Write(bytes, 0, bytes.Length);

            //yield return request.SendWebRequest();
            yield return request.GetResponse();

            res = new StreamReader(request.GetResponse().GetResponseStream()).ReadToEnd();
            Console.WriteLine(res);
            //JsonData data = JsonMapper.ToObject(request.downloadHandler.text);
            data = JsonMapper.ToObject(res);
            if (data.Keys.Contains("message"))
            {
                if (data["message"].Equals("Bad credentials"))
                    LogInfos("Wrong username or password.");
                else
                    LogInfos("A branch with the given name\n" +
                             "exists already on your account.");
                yield break;
            }

            LogInfos("Branch " + branchName + " was succesful created\n" +
                     "in repository " + GetRepositoryRemoteName(repo));

            Remote remote = repo.Network.Remotes["origin"];
            repo.Branches.Update(branch, b => b.Remote = remote.Name, b => b.UpstreamBranch = branch.CanonicalName);

        }

        public static string CreateAuthorizationString(string username, string password)
        {
            string authorization = username + ":" + password;
            authorization = "Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(authorization));
            return authorization;
        }

        public static string GetRepositoryRemoteName(Repository repo)
        {
            string repoURL = new List<Remote>(repo.Network.Remotes)[0].Url;
            string repoName = repoURL.Substring(repoURL.LastIndexOf('/') + 1).Replace(".git", "");
            return repoName;
        }

        public static void LogInfos(string text)
        {
            //MessageBoxScript.Message msg = new MessageBoxScript.Message(text, 1.5f, MessageBoxScript.PriorityType.Normal);
            //StolperwegeHelper.messageBox.AddMessage(msg);
            Console.WriteLine(text);
        }
    }
}
