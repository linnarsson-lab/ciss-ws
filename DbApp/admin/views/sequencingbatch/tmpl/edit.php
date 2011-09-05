<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
$params = $this->form->getFieldsets('params');
?>

<form action="<?php echo JRoute::_('index.php?option=com_dbapp&layout=edit&id='.(int) $this->item->id); ?>" method="post" name="adminForm" id="sequencingbatch-form" class="form-validate">

	<div class="width-60 fltlft">
		<fieldset class="adminform">
			<legend><?php echo JText::_( 'DbApp Sequencing Batch Details' ); ?></legend>
			<ul class="adminformlist">
<?php //JError::raiseWarning('500', JText::_('contact edit view ' . JRequest::getVar('view') . '  > |')); ?>
<?php foreach($this->form->getFieldset('sequencingbatchdetails') as $field): ?>
				<li><?php echo $field->label;echo $field->input;?></li>
<?php endforeach; ?>
			</ul>
	</div>

  <div class="width-40 fltrt">
    <?php echo JHtml::_('sliders.start', 'sequencingbatch-slider'); ?>
<?php foreach ($params as $name => $fieldset): ?>
    <?php echo JHtml::_('sliders.panel', JText::_($fieldset->label), $name.'-params');?>
  <?php if (isset($fieldset->description) && trim($fieldset->description)): ?>
    <p class="tip"><?php echo $this->escape(JText::_($fieldset->description));?></p>
  <?php endif;?>
    <fieldset class="panelform" >
      <ul class="adminformlist">
  <?php foreach ($this->form->getFieldset($name) as $field) : ?>
        <li><?php echo $field->label; ?><?php echo $field->input; ?></li>
  <?php endforeach; ?>
      </ul>
    </fieldset>
<?php endforeach; ?>
    <?php echo JHtml::_('sliders.end'); ?>
  </div>

	<div>
		<input type="hidden" name="task" value="sequencingbatch.edit" />
		<?php echo JHtml::_('form.token'); ?>
	</div>

</form>
