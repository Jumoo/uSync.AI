import { AIPublishEntityAction, type AIPublishMode } from "./ai-publish.action.js";

export default class AIPushEntityAction extends AIPublishEntityAction {
  protected mode: AIPublishMode = "Push";
}
