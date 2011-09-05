<?php
defined('_JEXEC') or die;
jimport('joomla.form.helper');
JFormHelper::loadFieldClass('list');

class JFormFieldSequencingPrimer extends JFormFieldList {

	protected $type = 'SequencingPrimer';

	protected function getOptions() {
		$db = JFactory::getDBO();
		$query = new JDatabaseQuery;
		$query->select(' id, primername, sequence, comment, user, time');
		$query->from('#__aaasequencingprimer');
		$db->setQuery((string)$query);
		$messages = $db->loadObjectList();

		$options = array();
		if ($messages) {
			foreach($messages as $message) {
				$options[] = JHtml::_('select.option', $message->id, $message->primername, $message->sequence,  $message->comment, $message->user, $message->time);
			}
		}
		$options = array_merge(parent::getOptions(), $options);
		return $options;
	}
}
