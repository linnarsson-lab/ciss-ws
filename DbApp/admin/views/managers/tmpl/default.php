<?php

defined('_JEXEC') or die('Restricted Access');
JHtml::_('behavior.tooltip');

?>
<form action="<?php echo JRoute::_('index.php?option=com_dbapp'); ?>" method="post" name="adminForm">
	<table class="adminlist">

<tr>
  <th width="5">
    <?php echo JText::_('Manager ID'); ?>
  </th>
  <th width="20">
    <input type="checkbox" name="toggle" value="" onclick="checkAll(<?php echo count($this->items); ?>);" />
  </th>     
  <th>
    <?php echo JText::_('Manager'); ?> 
  </th>
  <th>
    <?php echo JText::_('Email'); ?>
  </th>
  <th>
    <?php echo JText::_('Phone'); ?>
  </th>
  <th>
    <?php echo JText::_('User'); ?>
  </th>
  <th>
    <?php echo JText::_('Time'); ?>
  </th>
</tr>

<?php foreach($this->items as $i => $item): ?>
  <tr class="row<?php echo $i % 2; ?>">
    <td>
      <?php echo $item->id; ?>
    </td>
    <td>
      <?php echo JHtml::_('grid.id', $i, $item->id); ?>
    </td>
    <td>
      <a href="<?php echo JRoute::_('index.php?option=com_dbapp&task=manager.edit&id=' . $item->id); ?>">
        <?php echo $item->person; ?>
      </a>
    </td>
    <td>
        <?php echo $item->email; ?>
    </td>
    <td>
      <?php echo $item->phone; ?>
    </td>
    <td>
      <?php echo $item->user; ?>
    </td>
    <td>
      <?php echo $item->time; ?>
    </td>
  </tr>
<?php endforeach; ?>

<tr>
  <td colspan="7"><?php echo $this->pagination->getListFooter(); ?></td>
</tr>

	</table>
	<div>
		<input type="hidden" name="task" value="" />
    <input type="hidden" name="view" value="managers" />
		<input type="hidden" name="boxchecked" value="0" />
		<?php echo JHtml::_('form.token'); ?>
	</div>
</form>
