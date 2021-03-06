<?php

defined('_JEXEC') or die('Restricted Access');
JHtml::_('behavior.tooltip');

?>
<form action="<?php echo JRoute::_('index.php?option=com_dbapp'); ?>" method="post" name="adminForm">
	<table class="adminlist">

<tr>
  <th width="5">
    <?php echo JText::_('Seq Batch ID'); ?>
  </th>
  <th width="20">
    <input type="checkbox" name="toggle" value="" onclick="checkAll(<?php echo count($this->items); ?>);" />
  </th>     
  <th>
    <?php echo JText::_('Planned no of lanes'); ?>
  </th>
  <th>
    <?php echo JText::_('Planned no of cycles'); ?>
  </th>
  <th>
    <?php echo JText::_('Cost'); ?>
  </th>
  <th>
    <?php echo JText::_('Invoice'); ?>
  </th>
  <th>
    <?php echo JText::_('Signed'); ?>
  </th>
  <th>
    <?php echo JText::_('Comment'); ?>
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
      <a href="<?php echo JRoute::_('index.php?option=com_dbapp&task=sequencingbatch.edit&id=' . $item->id); ?>">
        <?php echo $item->plannednumberoflanes; ?>
      </a>
    </td>
    <td>
        <?php echo $item->plannednumberofcycles; ?>
    </td>
    <td>
        <?php echo $item->cost; ?>
    </td>
    <td>
        <?php echo $item->invoice; ?>
    </td>
    <td>
        <?php echo $item->signed; ?>
    </td>
    <td>
      <?php echo $item->comment; ?>
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
  <td colspan="9"><?php echo $this->pagination->getListFooter(); ?></td>
</tr>

	</table>
	<div>
		<input type="hidden" name="task" value="" />
    <input type="hidden" name="view" value="sequencingbatches" />
		<input type="hidden" name="boxchecked" value="0" />
		<?php echo JHtml::_('form.token'); ?>
	</div>
</form>
