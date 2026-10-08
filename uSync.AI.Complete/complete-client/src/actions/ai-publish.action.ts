import { UmbEntityActionBase, UmbEntityUpdatedEvent } from "@umbraco-cms/backoffice/entity-action";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import { UMB_MODAL_MANAGER_CONTEXT, UmbModalToken } from "@umbraco-cms/backoffice/modal";
import { umbHttpClient } from "@umbraco-cms/backoffice/http-client";

export type AIPublishMode = "Push" | "Pull";

type SyncItem = { name: string; udi: string };

// uSync.Publisher's own push/pull dialog, opened by alias. Its client isn't published as a
// package we can import from, so the token is rebuilt here with the same alias and shape.
const USYNC_PUBLISHER_PROCESS_MODAL = new UmbModalToken<
  { name?: string; items: Array<unknown>; entityType: string | null; mode: AIPublishMode },
  { success: boolean }
>("usync-publisher-process-modal", { modal: { type: "sidebar", size: "small" } });

/**
 * Push or pull an Umbraco.AI item with uSync.Complete.
 *
 * uSync.Publisher's own entity actions can't be reused for these items. They treat the
 * backoffice entity type as a UDI entity type, and Umbraco.AI's ("uai:agent") isn't a valid
 * one: the server finds no UDI to check, reports no servers, and the client caches that empty
 * list against the entity type, so the dialog has nothing to offer.
 *
 * This action does the same job, but asks uSync for the item first and hands the dialog the
 * entity type from the UDI that comes back ("umbraco-ai-agent"), which everything downstream
 * understands. See UPSTREAM-REQUESTS.md.
 */
export abstract class AIPublishEntityAction extends UmbEntityActionBase<never> {
  protected abstract mode: AIPublishMode;

  override async execute(): Promise<void> {
    const unique = this.args.unique;
    if (!unique) return;

    // the uSync.AI item manager for this entity type turns the id into a sync item
    const response = await umbHttpClient.get({
      // without this the client sends no credentials, and the 401 logs the user out
      security: [{ scheme: "bearer", type: "http" }],
      url: "/umbraco/usync/api/v1/Publisher/GetSyncItem",
      query: { entityType: this.args.entityType, id: unique },
    });
    const item = response.data as SyncItem | undefined;
    if (!item?.udi) return;

    const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
    if (!modalManager) return;

    const modal = modalManager.open(this, USYNC_PUBLISHER_PROCESS_MODAL, {
      data: {
        name: item.name,
        items: [item.udi],
        entityType: udiEntityType(item.udi),
        mode: this.mode,
      },
    });

    const completed = await modal
      .onSubmit()
      .then(() => true)
      .catch(() => false); // false when the user pressed cancel.

    // a pull overwrites the local item, so an open editor is now showing stale data.
    if (completed && this.mode === "Pull") {
      const events = await this.getContext(UMB_ACTION_EVENT_CONTEXT);
      events?.dispatchEvent(new UmbEntityUpdatedEvent({ unique, entityType: this.args.entityType }));
    }
  }
}

/** "umb://umbraco-ai-agent/3f0c..." -> "umbraco-ai-agent" */
const udiEntityType = (udi: string) => udi.split("/")[2] ?? null;
