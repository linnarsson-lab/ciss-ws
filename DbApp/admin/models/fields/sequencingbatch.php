<?php
defined('_JEXEC') or die;
jimport('joomla.form.helper');
JFormHelper::loadFieldClass('list');

class JFormFieldSequencingBatch extends JFormFieldList {

	protected $type = 'SequencingBatch';

	protected function getOptions() {
		$db = JFactory::getDBO();
		$query = new JDatabaseQuery;
		$query->select('id, plannednumberoflanes, plannednumberofcycles, cost, invoice, signed, comment, user, time');
		$query->from('#__aaasequencingbatch');
		$db->setQuery((string)$query);
		$messages = $db->loadObjectList();

		$options = array();
		if ($messages) {
			foreach($messages as $message) {
				$options[] = JHtml::_('select.option', $message->id, $message->plannednumberoflanes, $message->plannednumberofcycles, $message->cost, $message->invoice, $message->signed, $message->comment, $message->user, $message->time);
			}
		}
		$options = array_merge(parent::getOptions(), $options);
		return $options;
	}
}

