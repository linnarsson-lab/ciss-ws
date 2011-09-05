<?php

defined('_JEXEC') or die('Restricted Access');
JHtml::_('behavior.tooltip');

?>
<form action="<?php echo JRoute::_('index.php?option=com_dbapp'); ?>" method="post" name="adminForm">
	<table class="adminlist">

<tr>
  <th width="5">
    <?php echo JText::_('Client&nbsp;ID'); ?>
  </th>
  <th width="20">
    <input type="checkbox" name="toggle" value="" onclick="checkAll(<?php echo count($this->items); ?>);" />
  </th>     
  <th>
    <?php echo JText::_('Principal&nbsp;investigator&nbsp;'); ?>
  </th>
  <th>
    <?php echo JText::_('&nbsp;Category&nbsp;'); ?>
  </th>
  <th>
    <?php echo JText::_('&nbsp;Department&nbsp;'); ?>
  </th>
  <th>
    <?php echo JText::_('&nbsp;Address&nbsp;'); ?>
  </th>
  <th>
    <?php echo JText::_('&nbsp;Vat&nbsp;No&nbsp;'); ?>
  </th>
  <th>
    <?php echo JText::_('&nbsp;Comments&nbsp;'); ?>
  </th>
  <th>
    <?php echo JText::_('&nbsp;User&nbsp;'); ?>
  </th>
  <th>
    <?php echo JText::_('&nbsp;Date&nbsp;'); ?>
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
      <a href="<?php echo JRoute::_('index.php?option=com_dbapp&task=client.edit&id=' . $item->id); ?>">
        <?php echo $item->principalinvestigator; ?>
      </a>
    </td>
    <td>
      <?php echo $item->category; ?>
    </td>
    <td>
      <?php echo $item->department; ?>
    </td>
    <td>
      <?php echo $item->address; ?>
    </td>
    <td>
      <?php echo $item->vatno; ?>
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
  <td colspan="11"><?php echo $this->pagination->getListFooter(); ?></td>
</tr>

	</table>
	<div>
		<input type="hidden" name="task" value="" />
		<input type="hidden" name="boxchecked" value="0" />
		<?php echo JHtml::_('form.token'); ?>
	</div>
</form>
