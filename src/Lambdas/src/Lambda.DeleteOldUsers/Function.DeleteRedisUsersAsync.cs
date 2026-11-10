using Amazon.ElastiCache.Model;
using Amazon.Lambda.Core;
using Lambda.DeleteOldConnections.Structures;
using Shared.Lambda.Structures;

namespace Lambda.DeleteOldConnections
{
    public partial class Function
    {
        private async Task DeleteRedisUsersAsync(
            List<OldUsers.RedisUser> users,
            DeletionResults deletionResults,
            ILambdaLogger logger)
        {
            // The users are grouped by their group IDs to make fewer API calls to AWS.
            // These API calls are for stopping new logins.
            var groupedUsers = users.GroupBy(u => u.UserGroupId);
            foreach (var group in groupedUsers)
            {
                var groupResponse = await _awsElastiCache.ModifyUserGroupAsync(new ModifyUserGroupRequest
                {
                    UserGroupId = group.Key,
                    UserIdsToRemove = group.Select(u => u.Username).ToList()
                });

                logger.LogInformation($"Redis - All old users are removed from the {group.Key} group.");
            }

            // Wait for groups to be active again
            var groups = groupedUsers.Select(g => g.Key).GetEnumerator();
            groups.MoveNext();
            while (true)
            {
                var groupResponse = await _awsElastiCache.DescribeUserGroupsAsync(new DescribeUserGroupsRequest()
                {
                    UserGroupId = groups.Current
                });

                var group = groupResponse.UserGroups.FirstOrDefault();
                if (group!.Status == "active" && !groups.MoveNext())
                    break;

                // Wait 1 second before making another API call
                await Task.Delay(1000);
            }

            // Delete users
            foreach (var user in users)
            {
                var id = $"{user.UserGroupId}/{user.Username}";

                var deleteResponse = await _awsElastiCache.DeleteUserAsync(new DeleteUserRequest()
                {
                    UserId = user.Username
                });

                var statusCodeInt = (int)deleteResponse.HttpStatusCode;
                if (statusCodeInt >= 200 && statusCodeInt < 300)
                {
                    deletionResults.DeletedRedisUsers.Add(id);

                    logger.LogInformation($"Redis ({id}) - The user has been deleted.");
                }
                else
                {
                    deletionResults.FailedRedisUsers.Add(id);

                    logger.LogInformation($"Redis ({id}) - Something went wrong. Status Code: {statusCodeInt}");
                }
            }
        }
    }
}
