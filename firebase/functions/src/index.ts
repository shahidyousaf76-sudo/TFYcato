// TFY Cat Adventure — Cloud Functions (trusted server operations).
// Players: anonymous Firebase Auth only. Admins: email accounts with a `role` custom claim.
import * as functionsV1 from 'firebase-functions/v1';
import { deletePlayerData } from './handlers/player';
import { REGION } from './util/firebase';

export { createPlayer, updateLoadout, deleteMyData } from './handlers/player';
export { completeLevel, syncOfflineRuns, cleanupRunRecords } from './handlers/progress';
export { getDailyReward, claimDailyReward, purchaseItem, openMysteryBox } from './handlers/rewards';
export {
  adminGetStats, adminUpsertLevel, adminSetLevelEnabled, adminUpsertContent, adminUpdateRewards, adminUpsertEvent,
  adminPublishAnnouncement, adminGetRemoteConfig, adminPublishRemoteConfig, adminReviewLeaderboard, adminSetRole,
  adminListAdmins, adminSeedContent, reportClientIssue,
} from './admin/admin';

/** If an account is deleted (e.g. from the console), its game data goes too. */
export const onAccountDeleted = functionsV1.region(REGION).auth.user().onDelete(async (user) => {
  await deletePlayerData(user.uid);
});
