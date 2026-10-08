import { AIPublishEntityAction, type AIPublishMode } from "./ai-publish.action.js";

export default class AIPullEntityAction extends AIPublishEntityAction {
  protected mode: AIPublishMode = "Pull";
}
