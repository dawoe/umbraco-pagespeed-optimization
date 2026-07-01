import { PageSpeedOptimizer } from '../../api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type {
  UmbConditionConfigBase,
  UmbConditionControllerArguments,
  UmbExtensionCondition,
} from '@umbraco-cms/backoffice/extension-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { tryExecute } from '@umbraco-cms/backoffice/resources';

export class ImageOptimizationEnabledCondition
  extends UmbConditionBase<UmbConditionConfigBase>
  implements UmbExtensionCondition
{
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UmbConditionConfigBase>) {
    super(host, args);

    tryExecute(this, PageSpeedOptimizer.getDefaultValues()).then(({ data }) => {
      this.permitted = data?.enabled ?? false;
    });
  }
}

export default ImageOptimizationEnabledCondition;
